import sys
import os
import pandas as pd

sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..")))
from bet_builder.db.client import BetBuilderDB
from bet_builder.config import get_supabase_client, MIN_EV_THRESHOLD

def calculate_ev_for_all(db: BetBuilderDB):
    """
    Finds all predictions that have corresponding odds, calculates the Edge (EV) and Kelly Criterion,
    and upserts into the value_bets table.
    """
    # 1. Get all scheduled matches with predictions
    preds_res = (
        db._client.table("model_predictions")
        .select("id, fixture_id, home_win_prob, draw_prob, away_win_prob, over_25_prob, over_15_prob, dc_1x_prob, dc_12_prob, dc_x2_prob")
        .execute()
    )
    if not preds_res.data:
        print("  No predictions found.")
        return
        
    predictions = {p["fixture_id"]: p for p in preds_res.data}
    fixture_ids = list(predictions.keys())
    
    # 2. Get all odds for these fixtures, including the bookmaker code so we can identify Pinnacle
    # We must batch this to avoid the 1000 row limit in Supabase!
    all_odds = []
    for i in range(0, len(fixture_ids), 50):
        batch = fixture_ids[i:i+50]
        if not batch: continue
        
        odds_res = (
            db._client.table("odds")
            .select("id, fixture_id, bookmaker_id, bookmakers(code), market, selection, price")
            .in_("fixture_id", batch)
            .execute()
        )
        if odds_res.data:
            all_odds.extend(odds_res.data)
            
    if not all_odds:
        print("  No odds found for the scheduled fixtures.")
        return
        
    # Group odds by (fixture_id, market, selection) to compare bookmakers
    odds_grouped = {}
    for odd in all_odds:
        key = (odd["fixture_id"], odd["market"], odd["selection"])
        if key not in odds_grouped:
            odds_grouped[key] = []
        odds_grouped[key].append(odd)
        
    # 3. Calculate EV with Pinnacle Validation
    value_bets = []
    
    for key, odd_list in odds_grouped.items():
        fix_id, m, s = key
        pred = predictions.get(fix_id)
        if not pred: continue
        
        # Map odd market/selection to prediction probability
        prob = 0.0
        if m == "1X2":
            if s == "H": prob = pred["home_win_prob"]
            elif s == "D": prob = pred["draw_prob"]
            elif s == "A": prob = pred["away_win_prob"]
        elif m == "OU25" and s == "Over": prob = pred["over_25_prob"]
        elif m == "OU25" and s == "Under": prob = 1.0 - pred["over_25_prob"]
        elif m == "OU15" and s == "Over": prob = pred["over_15_prob"]
        elif m == "OU15" and s == "Under": prob = 1.0 - pred["over_15_prob"]
        elif m == "DC":
            if s == "1X": prob = pred["dc_1x_prob"]
            elif s == "12": prob = pred["dc_12_prob"]
            elif s == "X2": prob = pred["dc_x2_prob"]
            
        if prob == 0.0: continue
        
        # Find Pinnacle Odds for this outcome
        pinnacle_odd = next((o for o in odd_list if o.get("bookmakers") and o["bookmakers"].get("code") == "pinnacle"), None)
        pinnacle_price = pinnacle_odd["price"] if pinnacle_odd else 0.0
        
        # Check all soft bookmakers against Pinnacle
        for odd in odd_list:
            is_pinnacle = odd.get("bookmakers") and odd["bookmakers"].get("code") == "pinnacle"
            if is_pinnacle: continue # We don't bet ON pinnacle, we bet against them
            
            bookie_odds = odd["price"]
            
            # PINNACLE VALIDATION RULE:
            # If Pinnacle odds exist, the soft bookie MUST offer a higher price than Pinnacle.
            if pinnacle_price > 0 and bookie_odds <= pinnacle_price:
                continue # Rejected by the sharp filter!
                
            # EV Formula: (Probability * Odds) - 1
            ev_percent = ((prob * bookie_odds) - 1.0) * 100
            
            # We only care about positive EV according to our model
            if ev_percent > MIN_EV_THRESHOLD:
                b = bookie_odds - 1.0
                kelly_fraction = ((b * prob) - (1.0 - prob)) / b if b > 0 else 0
                quarter_kelly = round(max(0.0, (kelly_fraction / 4.0) * 100), 2)
                
                value_bets.append({
                    "fixture_id": fix_id,
                    "prediction_id": pred["id"],
                    "bookmaker_id": odd["bookmaker_id"],
                    "market": odd["market"],
                    "selection": odd["selection"],
                    "model_prob": prob,
                    "odds_price": bookie_odds,
                    "implied_prob": 1.0 / bookie_odds if bookie_odds > 0 else 0,
                    "edge_pct": (prob - (1.0 / bookie_odds)) * 100 if bookie_odds > 0 else 0,
                    "ev_pct": ev_percent,
                    "kelly_fraction": quarter_kelly
                })
            
    # 4. Upsert value bets
    
    # First, delete all existing value bets for these fixtures so we don't keep stale bets
    # if the odds dropped and they are no longer +EV!
    if fixture_ids:
        # We process in batches of 100 to avoid URL length limits in postgrest
        for i in range(0, len(fixture_ids), 100):
            batch = fixture_ids[i:i+100]
            db._client.table("value_bets").delete().in_("fixture_id", batch).execute()

    if value_bets:
        db._client.table("value_bets").upsert(value_bets, on_conflict="fixture_id,bookmaker_id,market,selection").execute()
        print(f"  [+] Saved {len(value_bets)} +EV value bets!")
    else:
        print("  [-] No value bets found matching the threshold.")

if __name__ == "__main__":
    db = BetBuilderDB(get_supabase_client())
    calculate_ev_for_all(db)
