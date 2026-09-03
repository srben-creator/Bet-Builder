import sys
import os

# Ensure we can import bet_builder
sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..")))

from bet_builder.config import get_supabase_client
from bet_builder.db.client import BetBuilderDB

# Conversion rate: 1 Shot on Target = 0.30 xG
SOT_CONVERSION_RATE = 0.30

class SOTProxy:
    def __init__(self, db: BetBuilderDB):
        self.db = db

    def run(self):
        print("Starting SOT Proxy calculation for all leagues...")
        
        # 1. Get ALL Active Leagues
        leagues = self.db.get_leagues(active_only=True)
        if not leagues:
            print("  [!] No active leagues found in DB.")
            return
            
        for league in leagues:
            league_id = league["id"]
            print(f"  Processing {league['name']}...")
            
            # 2. Find all seasons for the league
            seasons_res = self.db._client.table("seasons").select("id").eq("league_id", league_id).execute()
            if not seasons_res.data:
                print(f"    [!] No seasons found for {league['name']}.")
                continue
                
            season_ids = [s["id"] for s in seasons_res.data]
            
            # 3. Fetch fixtures for these seasons that have SOT data but no xG
            fixtures_res = (
                self.db._client.table("fixtures")
                .select("id, home_sot, away_sot, home_xg")
                .in_("season_id", season_ids)
                .not_.is_("home_sot", "null")
                .execute()
            )
            
            fixtures = fixtures_res.data
            if not fixtures:
                print("    ✅ No missing SOT-proxy xG to calculate.")
                continue
                
            updates_count = 0
            for f in fixtures:
                # If xG is completely missing or explicitly zero (unlikely in real matches)
                if f.get("home_xg") is None or str(f.get("home_xg")) == "0.0" or f.get("xg_source") == "manual":
                    home_sot = f.get("home_sot", 0) or 0
                    away_sot = f.get("away_sot", 0) or 0
                    
                    home_xg_proxy = round(home_sot * SOT_CONVERSION_RATE, 2)
                    away_xg_proxy = round(away_sot * SOT_CONVERSION_RATE, 2)
                    
                    self.db._client.table("fixtures").update({
                        "home_xg": home_xg_proxy,
                        "away_xg": away_xg_proxy,
                        "xg_source": "manual"
                    }).eq("id", f["id"]).execute()
                    
                    updates_count += 1
                    
            print(f"    ✅ Updated {updates_count} fixtures with SOT-proxy xG.")

if __name__ == "__main__":
    client = get_supabase_client()
    db = BetBuilderDB(client)
    proxy = SOTProxy(db)
    proxy.run()
