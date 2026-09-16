import sys
import os
import pandas as pd
from datetime import datetime, timedelta
import time
from typing import Dict, List, Any

# Ensure we can import bet_builder
sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..")))

from bet_builder.config import get_supabase_client, MIN_EV_THRESHOLD
from bet_builder.db.client import BetBuilderDB
from bet_builder.model.math_engine import DixonColesMathEngine
from bet_builder.model.predictor import BetBuilderPredictor

class Backtester:
    def __init__(self, db: BetBuilderDB, league_code: str = "E0"):
        self.db = db
        self.client = db._client
        self.league_code = league_code # e.g. "E0" for EPL

    def run(self):
        print(f"Starting Backtester for League: {self.league_code}")
        
        # 1. Fetch the League ID
        league_res = self.client.table("leagues").select("id").eq("fd_csv_code", self.league_code).execute()
        if not league_res.data:
            print("League not found.")
            return
        league_id = league_res.data[0]["id"]
        
        # 2. Fetch all completed fixtures with xG for this league, sorted by date
        # We need to simulate betting in chronological order
        predictor = BetBuilderPredictor(self.db)
        df_all = predictor._get_training_data(league_id)
        if df_all.empty:
            print("No training data found.")
            return
            
        # Sort by match_date ascending to simulate time
        df_all["match_date"] = pd.to_datetime(df_all["match_date"]).dt.tz_localize(None)
        df_all = df_all.sort_values("match_date")
        
        # Define simulation window (e.g. start betting from the 23/24 season)
        # We will use the first 1000 matches (approx 2.5 seasons) as the initial training set
        MIN_TRAIN_MATCHES = 380 * 2 # Require at least 2 seasons of data before we start betting
        
        if len(df_all) <= MIN_TRAIN_MATCHES:
            print(f"Not enough historical data. Found {len(df_all)} matches, need {MIN_TRAIN_MATCHES}.")
            return
            
        # We will train the model once a week (e.g. every Monday) to save time
        current_train_date = df_all.iloc[MIN_TRAIN_MATCHES]["match_date"]
        # Set to the previous Monday
        current_train_date = current_train_date - timedelta(days=current_train_date.weekday())
        
        # Fetch bookmakers
        bookmakers_res = self.client.table("bookmakers").select("id, code").execute()
        self.bookmakers = {b["code"]: b["id"] for b in bookmakers_res.data}
        self.pinnacle_closing_id = self.bookmakers.get("PSC") or self.bookmakers.get("pinnacle")
        self.b365_id = self.bookmakers.get("B365")
        
        if not self.b365_id or not self.pinnacle_closing_id:
            print("Missing B365 or Pinnacle Closing bookmaker in DB. Run football_data.py first.")
            return
            
        print(f"Simulation starting on {current_train_date.date()}...")
        
        engine = DixonColesMathEngine(decay_rate=0.0065)
        
        total_bets = 0
        profit = 0.0
        
        # Iterate over weeks
        end_date = df_all["match_date"].max()
        
        # Store results for bulk insert
        backtest_results_batch = []
        
        while current_train_date <= end_date:
            next_week = current_train_date + timedelta(days=7)
            
            # 1. Train Model using data strictly BEFORE current_train_date
            df_train = df_all[df_all["match_date"] < current_train_date].copy()
            
            # Recalculate 'days_ago' relative to current_train_date (no data leakage)
            df_train["days_ago"] = (current_train_date - df_train["match_date"]).dt.days
            
            # Fit model
            print(f"\n--- Week of {current_train_date.date()} ---")
            engine.fit(df_train)
            
            # 2. Predict matches occurring in this week
            df_week = df_all[(df_all["match_date"] >= current_train_date) & (df_all["match_date"] < next_week)]
            
            if not df_week.empty:
                fixture_ids_str = "','".join(df_week["fixture_id"].astype(str).tolist())
                # Fetch all odds for these matches
                odds_res = self.client.table("odds").select("*").in_("fixture_id", df_week["fixture_id"].tolist()).execute()
                week_odds = pd.DataFrame(odds_res.data)
                
                if not week_odds.empty:
                    for _, match in df_week.iterrows():
                        fixture_id = match["fixture_id"]
                        home_team = match["home_team"]
                        away_team = match["away_team"]
                        
                        try:
                            results = engine.predict_match(home_team, away_team)
                        except ValueError:
                            # Team not in training set (e.g. newly promoted team without prior history)
                            continue
                            
                        # Check for value bets
                        match_odds = week_odds[week_odds["fixture_id"] == fixture_id]
                        if match_odds.empty:
                            continue
                            
                        self._process_match_bets(match, results["probabilities"], match_odds, backtest_results_batch)
                        
                        # Bulk insert if batch gets large
                        if len(backtest_results_batch) > 100:
                            self.client.table("backtest_results").insert(backtest_results_batch).execute()
                            backtest_results_batch = []
            
            current_train_date = next_week
            
        # Final insert
        if backtest_results_batch:
            self.client.table("backtest_results").insert(backtest_results_batch).execute()
            
        print(f"\nBacktest complete! Simulated {total_bets} matches.")

    def _process_match_bets(self, match: pd.Series, probs: dict, match_odds: pd.DataFrame, batch: list):
        fixture_id = match["fixture_id"]
        hg = match["home_goals"]
        ag = match["away_goals"]
        
        # We'll simulate betting on B365 odds for this backtest
        b365_odds = match_odds[match_odds["bookmaker_id"] == self.b365_id]
        pinnacle_odds = match_odds[match_odds["bookmaker_id"] == self.pinnacle_closing_id]
        
        if b365_odds.empty:
            return
            
        # Iterate through the outcomes
        for market, sel, prob_val in [
            ("1X2", "H", probs["1X2"]["H"]),
            ("1X2", "D", probs["1X2"]["D"]),
            ("1X2", "A", probs["1X2"]["A"]),
            ("OU25", "Over", probs["OU25"]["Over"]),
            ("OU25", "Under", probs["OU25"]["Under"])
        ]:
            b365_price_row = b365_odds[(b365_odds["market"] == market) & (b365_odds["selection"] == sel)]
            pin_price_row = pinnacle_odds[(pinnacle_odds["market"] == market) & (pinnacle_odds["selection"] == sel)]
            
            if b365_price_row.empty:
                continue
                
            price = float(b365_price_row.iloc[0]["price"])
            pin_price = float(pin_price_row.iloc[0]["price"]) if not pin_price_row.empty else None
            
            # Sharp filter: Only bet if B365 is higher than Pinnacle Closing
            if pin_price and price <= pin_price:
                continue
                
            ev_pct = ((prob_val * price) - 1.0) * 100
            
            if ev_pct > MIN_EV_THRESHOLD:
                # Calculate Kelly
                b = price - 1.0
                kelly_fraction = ((b * prob_val) - (1.0 - prob_val)) / b if b > 0 else 0
                q_kelly = round(max(0.0, (kelly_fraction / 4.0) * 100), 2)
                
                if q_kelly <= 0:
                    continue
                    
                # Did it win?
                won = False
                if market == "1X2":
                    if hg > ag and sel == "H": won = True
                    elif hg == ag and sel == "D": won = True
                    elif hg < ag and sel == "A": won = True
                elif market == "OU25":
                    total_goals = hg + ag
                    if sel == "Over" and total_goals > 2.5: won = True
                    elif sel == "Under" and total_goals < 2.5: won = True
                    
                pnl = q_kelly * (price - 1.0) if won else -q_kelly
                
                clv_pct = None
                if pin_price:
                    # CLV edge over closing line
                    clv_pct = ((price / pin_price) - 1.0) * 100
                    
                batch.append({
                    "fixture_id": fixture_id,
                    "market": market,
                    "selection": sel,
                    "model_prob": prob_val,
                    "ev_pct": ev_pct,
                    "kelly_fraction": q_kelly,
                    "bookmaker_id": self.b365_id,
                    "odds_price": price,
                    "pinnacle_closing": pin_price,
                    "clv_pct": clv_pct,
                    "result_won": won,
                    "pnl": pnl
                })

if __name__ == "__main__":
    from bet_builder.etl.constants import FD_LEAGUES
    db = BetBuilderDB(get_supabase_client())
    
    # Clear existing backtest results once before looping
    print("Clearing previous backtest results...")
    db._client.table("backtest_results").delete().neq("id", "00000000-0000-0000-0000-000000000000").execute()
    
    # We will loop through all 6 leagues
    for code in FD_LEAGUES.values():
        try:
            backtester = Backtester(db, code)
            backtester.run()
        except Exception as e:
            print(f"Error running backtest for {code}: {e}")

