import sys
import os
import time
from datetime import datetime
import pandas as pd
from playwright.sync_api import sync_playwright

sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..")))
from bet_builder.config import get_supabase_client
from bet_builder.db.client import BetBuilderDB
from bet_builder.etl.team_mapping import get_canonical_name_fbref
from bet_builder.etl.constants import SEASONS_TO_INGEST, CURRENT_SEASON_START_YEAR

# Mapping of league id to FBref comp ID and slug
FBREF_LEAGUES = {
    "c6a910c2-d894-44ed-ab03-d3944d5a28eb": {"id": 9, "slug": "Premier-League"},  # EPL
    "4c3bc9b4-1f1c-4eb3-8bcc-48699bf439cd": {"id": 12, "slug": "La-Liga"},        # La Liga
    "a2d925b8-792e-40c1-a50a-261e177d7236": {"id": 11, "slug": "Serie-A"},        # Serie A
    "8f40c99b-d1c3-4a96-b48c-d18ccb456419": {"id": 13, "slug": "Ligue-1"},        # Ligue 1
    "b7e7a235-499a-4e52-849e-02446d8a4771": {"id": 32, "slug": "Primeira-Liga"},  # Primeira Liga
    "b8b930d6-1d14-4980-986f-f1283c82db1f": {"id": 20, "slug": "Bundesliga"}      # Bundesliga
}

class FBrefScraper:
    def __init__(self, db: BetBuilderDB):
        self.db = db
        self.teams_cache = {}

    def _get_team_id(self, raw_name: str, country: str) -> str:
        if pd.isna(raw_name): return None
        canonical_name = get_canonical_name_fbref(raw_name)
        cache_key = f"{canonical_name}_{country}"
        if cache_key in self.teams_cache:
            return self.teams_cache[cache_key]
        res = self.db._client.table("teams").select("id").eq("name", canonical_name).eq("country", country).execute()
        if res.data:
            tid = res.data[0]["id"]
            self.teams_cache[cache_key] = tid
            return tid
        return None

    def run(self):
        print("Starting FBref xG Scraper (Playwright)...")
        leagues = self.db.get_leagues(active_only=True)
        
        with sync_playwright() as p:
            browser = p.chromium.launch(headless=True)
            context = browser.new_context(user_agent="Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36")
            page = context.new_page()
            
            for league in leagues:
                l_id = league["id"]
                if l_id not in FBREF_LEAGUES:
                    continue
                fb_info = FBREF_LEAGUES[l_id]
                
                print(f"\nProcessing FBref xG for: {league['name']}")
                
                # Check for fast mode to skip historical seasons
                if hasattr(self, 'fast_mode') and self.fast_mode:
                    start_year = CURRENT_SEASON_START_YEAR
                    print("  [Fast Mode] Skipping historical seasons, only scraping current season.")
                else:
                    start_year = CURRENT_SEASON_START_YEAR - SEASONS_TO_INGEST + 1
                
                for year in range(start_year, CURRENT_SEASON_START_YEAR + 1):
                    # For FBref, season years are e.g. "2024-2025"
                    season_str = f"{year}-{year+1}"
                    # e.g. https://fbref.com/en/comps/9/2024-2025/schedule/2024-2025-Premier-League-Scores-and-Fixtures
                    url = f"https://fbref.com/en/comps/{fb_info['id']}/{season_str}/schedule/{season_str}-{fb_info['slug']}-Scores-and-Fixtures"
                    
                    print(f"  Fetching {season_str}...")
                    
                    try:
                        page.goto(url, wait_until="domcontentloaded")
                        # Check for cloudflare or block
                        if "Just a moment" in page.title():
                            print("    [!] Cloudflare block hit! Waiting 10s...")
                            time.sleep(10)
                        
                        html = page.content()
                        tables = pd.read_html(html)
                        
                        if not tables:
                            print("    [!] No tables found on page.")
                            time.sleep(4)
                            continue
                            
                        # Find the schedule table
                        df = None
                        for t in tables:
                            if "xG" in t.columns and "xG.1" in t.columns and "Home" in t.columns:
                                df = t
                                break
                                
                        if df is None:
                            print("    [!] xG columns not found in any table.")
                            time.sleep(4)
                            continue
                            
                        # Get season ID from DB
                        res = self.db._client.table("seasons").select("id").eq("league_id", l_id).eq("start_year", year).execute()
                        if not res.data:
                            print(f"    [!] Season {year} not found in DB.")
                            continue
                        season_id = res.data[0]["id"]
                        
                        updates_count = 0
                        for _, row in df.iterrows():
                            # Skip unplayed matches
                            if pd.isna(row.get("xG")) or pd.isna(row.get("xG.1")):
                                continue
                                
                            home_id = self._get_team_id(row["Home"], league["country"])
                            away_id = self._get_team_id(row["Away"], league["country"])
                            if not home_id or not away_id:
                                continue
                                
                            home_xg = float(row["xG"])
                            away_xg = float(row["xG.1"])
                            
                            # Update fixture by matching teams in this season
                            fixture_res = self.db._client.table("fixtures").select("id, home_xg").eq("season_id", season_id).eq("home_team_id", home_id).eq("away_team_id", away_id).execute()
                            if fixture_res.data:
                                fix_id = fixture_res.data[0]["id"]
                                current_xg = fixture_res.data[0].get("home_xg")
                                if current_xg is None or abs(float(current_xg) - home_xg) > 0.01:
                                    self.db._client.table("fixtures").update({
                                        "home_xg": home_xg,
                                        "away_xg": away_xg,
                                        "xg_source": "fbref"
                                    }).eq("id", fix_id).execute()
                                    updates_count += 1
                                    
                        print(f"    ✅ Updated {updates_count} fixtures with xG.")
                        
                    except Exception as e:
                        print(f"    [!] Error: {e}")
                        
                    time.sleep(5) # Polite delay
                    
            browser.close()
            print("\n[+] FBref xG Scraper completed.")

if __name__ == "__main__":
    import argparse
    parser = argparse.ArgumentParser(description="FBref xG Scraper")
    parser.add_argument("--fast", action="store_true", help="Only scrape the current season")
    args = parser.parse_args()

    client = get_supabase_client()
    db = BetBuilderDB(client)
    scraper = FBrefScraper(db)
    scraper.fast_mode = args.fast
    scraper.run()
