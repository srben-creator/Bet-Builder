import sys
import os
import pandas as pd
from datetime import datetime

# Ensure we can import bet_builder
sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..")))

from bet_builder.config import get_supabase_client
from bet_builder.db.client import BetBuilderDB
from bet_builder.model.math_engine import DixonColesMathEngine

class BetBuilderPredictor:
    def __init__(self, db: BetBuilderDB):
        self.db = db

    def _get_training_data(self, league_id: str) -> pd.DataFrame:
        """
        Fetches historical completed matches with xG data for a league.
        """
        # Find seasons for this league
        seasons_res = self.db._client.table("seasons").select("id").eq("league_id", league_id).execute()
        if not seasons_res.data:
            return pd.DataFrame()
            
        season_ids = [s["id"] for s in seasons_res.data]
        
        # Get completed fixtures with xG and actual goals
        res = (
            self.db._client.table("fixtures")
            .select("id, home_team_id, away_team_id, match_date, home_xg, away_xg, home_goals, away_goals")
            .in_("season_id", season_ids)
            .eq("status", "completed")
            .not_.is_("home_xg", "null")
            .execute()
        )
        
        if not res.data:
            return pd.DataFrame()
            
        df = pd.DataFrame(res.data)
        df.rename(columns={"id": "fixture_id"}, inplace=True)
        
        # Convert IDs to canonical strings for the engine (avoids UUID mismatch issues)
        df["home_team"] = df["home_team_id"].astype(str)
        df["away_team"] = df["away_team_id"].astype(str)
        
        # Calculate days ago for time decay
        df["match_date"] = pd.to_datetime(df["match_date"])
        df["days_ago"] = (pd.Timestamp.now() - df["match_date"]).dt.days
        
        # Ensure xG is float
        df["home_xg"] = df["home_xg"].astype(float)
        df["away_xg"] = df["away_xg"].astype(float)
        
        # Calculate Finishing Variance (Adjusted xG)
        # We find the total goals vs xG for each team over this dataset to create a multiplier
        team_stats = {}
        for index, row in df.iterrows():
            ht, at = row["home_team"], row["away_team"]
            
            if ht not in team_stats: team_stats[ht] = {"goals": 0, "xg": 0}
            if at not in team_stats: team_stats[at] = {"goals": 0, "xg": 0}
            
            if pd.notnull(row.get("home_goals")):
                team_stats[ht]["goals"] += int(row["home_goals"])
                team_stats[ht]["xg"] += row["home_xg"]
                
            if pd.notnull(row.get("away_goals")):
                team_stats[at]["goals"] += int(row["away_goals"])
                team_stats[at]["xg"] += row["away_xg"]
                
        # Calculate ratio, capped at 0.7 to 1.3 to prevent extreme outliers
        team_ratios = {}
        for t, stats in team_stats.items():
            if stats["xg"] > 0:
                ratio = stats["goals"] / stats["xg"]
                team_ratios[t] = max(0.7, min(1.3, ratio))
            else:
                team_ratios[t] = 1.0
                
        # Apply axG
        df["home_xg"] = df.apply(lambda x: x["home_xg"] * team_ratios.get(x["home_team"], 1.0), axis=1)
        df["away_xg"] = df.apply(lambda x: x["away_xg"] * team_ratios.get(x["away_team"], 1.0), axis=1)
        
        return df

    def _get_upcoming_fixtures(self, league_id: str) -> list:
        """
        Fetches scheduled fixtures that need predictions.
        """
        seasons_res = self.db._client.table("seasons").select("id").eq("league_id", league_id).execute()
        if not seasons_res.data:
            return []
            
        season_ids = [s["id"] for s in seasons_res.data]
        
        res = (
            self.db._client.table("fixtures")
            .select("id, home_team_id, away_team_id, match_date")
            .in_("season_id", season_ids)
            .eq("status", "scheduled")
            .execute()
        )
        return res.data

    def _save_prediction(self, fixture_id: str, results: dict):
        """
        Upserts the math engine's output into the model_predictions table.
        """
        probs = results["probabilities"]
        
        payload = {
            "fixture_id": fixture_id,
            "model_version": "v1_dixon_coles",
            "predicted_home_goals": float(results["predicted_home_xg"]),
            "predicted_away_goals": float(results["predicted_away_xg"]),
            
            # 1X2
            "home_win_prob": float(probs["1X2"]["H"]),
            "draw_prob": float(probs["1X2"]["D"]),
            "away_win_prob": float(probs["1X2"]["A"]),
            
            # Derived
            "dc_1x_prob": float(probs["DC"]["1X"]),
            "dc_12_prob": float(probs["DC"]["12"]),
            "dc_x2_prob": float(probs["DC"]["X2"]),
            "over_15_prob": float(probs["OU15"]["Over"]),
            "over_25_prob": float(probs["OU25"]["Over"])
        }
        
        self.db._client.table("model_predictions").upsert(
            payload, on_conflict="fixture_id, model_version"
        ).execute()

    def run(self):
        print("Starting BetBuilder Predictor...")
        
        leagues = self.db.get_leagues(active_only=True)
        
        for league in leagues:
            print(f"\nEvaluating {league['name']}...")
            
            # 1. Fetch Training Data
            df_train = self._get_training_data(league["id"])
            if df_train.empty:
                print("  [!] No historical xG data found. Skipping.")
                continue
                
            print(f"  Loaded {len(df_train)} historical matches for training.")
            
            # 2. Train Model
            engine = DixonColesMathEngine(decay_rate=0.0065)
            engine.fit(df_train)
            
            # 3. Predict Upcoming Fixtures
            upcoming = self._get_upcoming_fixtures(league["id"])
            if not upcoming:
                print("  No scheduled fixtures to predict.")
                continue
                
            success_count = 0
            for fixture in upcoming:
                h_id = str(fixture["home_team_id"])
                a_id = str(fixture["away_team_id"])
                
                try:
                    # Run the math engine
                    results = engine.predict_match(h_id, a_id)
                    
                    # Save to DB
                    self._save_prediction(fixture["id"], results)
                    success_count += 1
                except ValueError as e:
                    print(f"  [!] Skipping fixture {fixture['id']}: {e}")
                    
            print(f"  Saved {success_count} predictions to the database!")

if __name__ == "__main__":
    client = get_supabase_client()
    db = BetBuilderDB(client)
    predictor = BetBuilderPredictor(db)
    predictor.run()
