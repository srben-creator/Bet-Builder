import sys
import os
import re
import json
import asyncio
import aiohttp
from datetime import datetime

# Ensure we can import bet_builder
sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..")))

from bet_builder.config import get_supabase_client, LEAGUES
from bet_builder.db.client import BetBuilderDB
from bet_builder.etl.constants import SEASONS_TO_INGEST, CURRENT_SEASON_START_YEAR
from bet_builder.etl.team_mapping import get_canonical_name_understat

class UnderstatScraper:
    def __init__(self, db: BetBuilderDB):
        self.db = db
        # Cache for teams to avoid redundant lookups
        self.teams_cache = {}

    async def fetch_league_season(self, session: aiohttp.ClientSession, league_name: str, year: int) -> list:
        url = f"https://understat.com/league/{league_name}/{year}"
        print(f"  Fetching {league_name} {year}...")
        
        try:
            async with session.get(url) as response:
                if response.status != 200:
                    print(f"    [!] Failed to fetch {url}: HTTP {response.status}")
                    return []
                html = await response.text()
                
                # Extract the JSON data from the script tags
                # Understat stores match data in a variable called datesData
                match = re.search(r"datesData\s*=\s*JSON\.parse\('([^']+)'\)", html)
                if not match:
                    print(f"    [!] Could not find datesData in {url}")
                    return []
                    
                # Decode the unicode escape sequences
                decoded_json = match.group(1).encode('utf-8').decode('unicode_escape')
                matches = json.loads(decoded_json)
                return matches
                
        except Exception as e:
            print(f"    [!] Error fetching {url}: {e}")
            return []

    def _get_team_id(self, raw_name: str, country: str) -> str | None:
        canonical_name = get_canonical_name_understat(raw_name)
        cache_key = f"{canonical_name}_{country}"
        
        if cache_key in self.teams_cache:
            return self.teams_cache[cache_key]
            
        # Look it up in the database
        res = self.db._client.table("teams").select("id").eq("name", canonical_name).eq("country", country).execute()
        if res.data:
            team_id = res.data[0]["id"]
            self.teams_cache[cache_key] = team_id
            return team_id
            
        print(f"    [!] Team not found in DB: {raw_name} -> {canonical_name}")
        return None

    async def process_league(self, session: aiohttp.ClientSession, league: dict):
        if not league.get("understat_name"):
            return
            
        print(f"\nProcessing Understat xG for: {league['name']}")
        start_year = CURRENT_SEASON_START_YEAR - SEASONS_TO_INGEST + 1
        
        for year in range(start_year, CURRENT_SEASON_START_YEAR + 1):
            # Understat uses the start year in the URL (e.g., 2024 for 24/25 season)
            matches = await self.fetch_league_season(session, league["understat_name"], year)
            if not matches:
                continue
                
            # Get the season ID from our DB
            res = self.db._client.table("seasons").select("id").eq("league_id", league["id"]).eq("start_year", year).execute()
            if not res.data:
                print(f"    [!] Season {year} not found in DB for {league['name']}")
                continue
            season_id = res.data[0]["id"]
            
            updates_count = 0
            
            for match in matches:
                if not match.get("isResult"):
                    continue # Match hasn't been played yet
                    
                raw_home = match["h"]["title"]
                raw_away = match["a"]["title"]
                
                home_team_id = self._get_team_id(raw_home, league["country"])
                away_team_id = self._get_team_id(raw_away, league["country"])
                
                if not home_team_id or not away_team_id:
                    continue
                    
                home_xg = float(match["xG"]["h"])
                away_xg = float(match["xG"]["a"])
                
                # To be absolutely safe, we match by season_id, home_team_id, away_team_id
                # (because match dates can sometimes differ slightly between sources)
                fixture_res = self.db._client.table("fixtures").select("id, home_xg").eq("season_id", season_id).eq("home_team_id", home_team_id).eq("away_team_id", away_team_id).execute()
                
                if fixture_res.data:
                    fixture_id = fixture_res.data[0]["id"]
                    current_home_xg = fixture_res.data[0].get("home_xg")
                    
                    # Only update if it's missing or different to save API calls
                    if current_home_xg is None or abs(float(current_home_xg) - home_xg) > 0.01:
                        self.db._client.table("fixtures").update({
                            "home_xg": home_xg,
                            "away_xg": away_xg,
                            "xg_source": "understat"
                        }).eq("id", fixture_id).execute()
                        updates_count += 1
                        
            print(f"    ✅ Season {year}/{year+1}: Updated {updates_count} fixtures with xG.")

    async def run(self):
        print("Starting Understat Scraper...")
        leagues = self.db.get_leagues(active_only=True)
        
        async with aiohttp.ClientSession() as session:
            # We process leagues sequentially to not hammer Understat too hard
            for league in leagues:
                await self.process_league(session, league)

if __name__ == "__main__":
    client = get_supabase_client()
    db = BetBuilderDB(client)
    scraper = UnderstatScraper(db)
    
    # Run the async loop
    if sys.platform == "win32":
        asyncio.set_event_loop_policy(asyncio.WindowsSelectorEventLoopPolicy())
    asyncio.run(scraper.run())
