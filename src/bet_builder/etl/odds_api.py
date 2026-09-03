import sys
import os
import requests
import difflib
from datetime import datetime

# Ensure we can import bet_builder
sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..")))

from bet_builder.config import get_supabase_client, ODDS_API_KEY
from bet_builder.db.client import BetBuilderDB
from bet_builder.model.predictor import BetBuilderPredictor
from bet_builder.model.ev_calculator import calculate_ev_for_all

# Mapping from our internal codes to The Odds API sport keys
ODDS_API_SPORTS = {
    "E0": "soccer_epl",
    "SP1": "soccer_spain_la_liga",
    "D1": "soccer_germany_bundesliga",
    "I1": "soccer_italy_serie_a",
    "F1": "soccer_france_ligue_one",
    "P1": "soccer_portugal_primeira_liga"
}

class OddsAPIClient:
    def __init__(self, db: BetBuilderDB):
        self.db = db
        self.api_key = ODDS_API_KEY
        if not self.api_key:
            raise ValueError("ODDS_API_KEY is not set in .env")
            
        # Pre-load canonical team names for fuzzy matching
        self.canonical_teams = []
        teams_res = self.db._client.table("teams").select("id, name").execute()
        for t in teams_res.data:
            self.canonical_teams.append({"id": t["id"], "name": t["name"]})
            
    def _fuzzy_match_team(self, odds_team_name):
        """Finds the closest canonical team name in our DB."""
        if not self.canonical_teams:
            return None
            
        canonical_names = [t["name"] for t in self.canonical_teams]
        
        # Exact match
        if odds_team_name in canonical_names:
            return odds_team_name
            
        # Fuzzy match
        matches = difflib.get_close_matches(odds_team_name, canonical_names, n=1, cutoff=0.5)
        if matches:
            return matches[0]
            
        return None
        
    def _get_team_id(self, team_name):
        matched = self._fuzzy_match_team(team_name)
        if not matched:
            return None
        for t in self.canonical_teams:
            if t["name"] == matched:
                return t["id"]
        return None

    def fetch_and_store(self, target_bookmaker="Pinnacle"):
        print("Fetching Live Upcoming Fixtures from The Odds API...")
        
        leagues = self.db.get_leagues(active_only=True)
        fixtures_added = 0
        odds_added = 0
        
        # 1. Get or create bookmaker
        bookie_res = self.db._client.table("bookmakers").upsert(
            {"name": target_bookmaker, "code": target_bookmaker}, on_conflict="name"
        ).execute()
        bookie_id = bookie_res.data[0]["id"]
        
        for league in leagues:
            sport_key = ODDS_API_SPORTS.get(league["fd_csv_code"])
            if not sport_key:
                continue
                
            print(f"  Fetching {league['name']}...")
            url = f"https://api.the-odds-api.com/v4/sports/{sport_key}/odds/?regions=eu&markets=h2h,totals&oddsFormat=decimal&apiKey={self.api_key}"
            
            response = requests.get(url)
            if response.status_code != 200:
                print(f"    [!] Failed to fetch {league['name']}: {response.text}")
                continue
                
            events = response.json()
            
            # Find the current season ID for this league
            season_res = self.db._client.table("seasons").select("id").eq("league_id", league["id"]).eq("is_current", True).execute()
            if not season_res.data:
                continue
            season_id = season_res.data[0]["id"]
            
            for event in events:
                home_team_id = self._get_team_id(event["home_team"])
                away_team_id = self._get_team_id(event["away_team"])
                
                if not home_team_id or not away_team_id:
                    print(f"    [!] Skipping {event['home_team']} vs {event['away_team']} (Team match failed)")
                    continue
                    
                if home_team_id == away_team_id:
                    print(f"    [!] Skipping {event['home_team']} vs {event['away_team']} (Matched to same DB ID)")
                    continue
                    
                match_date = event["commence_time"]
                
                # Time-travel bug fix: Do not fetch odds for matches that have already started 
                # or are starting within the next 30 minutes (to avoid live in-play odds)
                commence_dt = datetime.fromisoformat(match_date.replace("Z", "+00:00"))
                time_until_kickoff = (commence_dt - datetime.now(commence_dt.tzinfo)).total_seconds()
                
                if time_until_kickoff < 1800:
                    print(f"    [!] Skipping {event['home_team']} vs {event['away_team']} (Starts too soon or already started)")
                    continue

                # Insert Fixture
                fixture_payload = {
                    "season_id": season_id,
                    "home_team_id": home_team_id,
                    "away_team_id": away_team_id,
                    "match_date": match_date,
                    "status": "scheduled",
                }
                fix_res = self.db._client.table("fixtures").upsert(fixture_payload, on_conflict="home_team_id,away_team_id,match_date").execute()
                fixture_id = fix_res.data[0]["id"]
                fixtures_added += 1
                
                # Extract Odds for Target Bookmakers
                target_bookies_keys = ["pinnacle", "betsson", "williamhill", "marathonbet", "matchbook", "onexbet"]
                bookmakers = event.get("bookmakers", [])
                
                for b in bookmakers:
                    if b["key"] not in target_bookies_keys:
                        continue
                        
                    # Get or create bookmaker
                    bookie_res = self.db._client.table("bookmakers").upsert(
                        {"name": b["title"], "code": b["key"]}, on_conflict="name"
                    ).execute()
                    bookie_id = bookie_res.data[0]["id"]
                    
                    for market in b.get("markets", []):
                        market_key = market["key"] # h2h or totals
                        for outcome in market["outcomes"]:
                            selection = ""
                            m_type = ""
                            
                            if market_key == "h2h":
                                m_type = "1X2"
                                if outcome["name"] == event["home_team"]: selection = "H"
                                elif outcome["name"] == event["away_team"]: selection = "A"
                                elif outcome["name"] == "Draw": selection = "D"
                            elif market_key == "totals":
                                point_str = str(outcome['point']).replace(".", "")
                                if point_str not in ["15", "25"]:
                                    continue
                                m_type = f"OU{point_str}"
                                if outcome["name"] == "Over": selection = "Over"
                                if outcome["name"] == "Under": selection = "Under"
                                
                            if m_type and selection:
                                odd_payload = {
                                    "fixture_id": fixture_id,
                                    "bookmaker_id": bookie_id,
                                    "market": m_type,
                                    "selection": selection,
                                    "price": outcome["price"]
                                }
                                self.db._client.table("odds").upsert(odd_payload, on_conflict="fixture_id,bookmaker_id,market,selection,odds_type").execute()
                                odds_added += 1

        print(f"Downloaded {fixtures_added} scheduled fixtures and {odds_added} odds from The Odds API.")

if __name__ == "__main__":
    client = get_supabase_client()
    db = BetBuilderDB(client)
    
    # 1. Fetch live odds and fixtures
    api_client = OddsAPIClient(db)
    api_client.fetch_and_store()
    
    # 2. Run predictions for the newly added fixtures
    print("\nRunning Math Engine to predict new fixtures...")
    predictor = BetBuilderPredictor(db)
    predictor.run()
    
    # 3. Calculate Expected Value (EV)
    print("\nCalculating Expected Value (EV)...")
    calculate_ev_for_all(db)
    
    print("\n[+] Pipeline Complete! Live matches are ready in the Streamlit Dashboard.")
