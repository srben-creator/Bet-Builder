import sys
import os

sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..")))

from bet_builder.config import get_supabase_client
from bet_builder.db.client import BetBuilderDB

def settle_pending_bets():
    """Finds pending value bets for completed fixtures and settles them based on the results."""
    db = BetBuilderDB(get_supabase_client())
    
    print("Settling pending value bets...")
    
    # Get all pending bets and join with their fixture
    all_bets = []
    limit = 1000
    offset = 0
    while True:
        res = (
            db._client.from_("value_bets")
            .select("id, market, selection, recommended_stake, odds_price, fixtures!inner(id, status, home_goals, away_goals)")
            .eq("status", "pending")
            .eq("fixtures.status", "completed")
            .range(offset, offset + limit - 1)
            .execute()
        )
        if not res.data:
            break
        all_bets.extend(res.data)
        if len(res.data) < limit:
            break
        offset += limit
    
    if not all_bets:
        print("No pending bets to settle for completed fixtures.")
        return
        
    updates = []
    
    for bet in all_bets:
        fix = bet["fixtures"]
        hg = fix["home_goals"]
        ag = fix["away_goals"]
        
        # Determine actual result
        if hg is None or ag is None:
            continue
            
        market = bet["market"]
        sel = bet["selection"]
        
        won = False
        
        if market == "1X2":
            if hg > ag and sel == "Home": won = True
            elif hg == ag and sel == "Draw": won = True
            elif hg < ag and sel == "Away": won = True
        elif market == "DC":
            if hg >= ag and sel == "1X": won = True
            elif hg <= ag and sel == "X2": won = True
            elif hg != ag and sel == "12": won = True
        elif market in ["OU25", "OU15"]:
            total_goals = hg + ag
            line = float(market.replace("OU", "")) / 10.0 # e.g. "OU25" -> 2.5
            if sel == "Over" and total_goals > line: won = True
            elif sel == "Under" and total_goals < line: won = True
        elif market == "BTTS":
            if sel == "Yes" and hg > 0 and ag > 0: won = True
            elif sel == "No" and (hg == 0 or ag == 0): won = True
            
        stake = bet.get("recommended_stake") or 0.0
        odds = bet.get("odds_price", 1.0)
        
        if won:
            pnl = stake * (odds - 1.0)
            status = "won"
        else:
            pnl = -stake
            status = "lost"
            
        updates.append({
            "id": bet["id"],
            "status": status,
            "pnl": round(pnl, 2),
            "actual_result": f"{hg}-{ag}"
        })
        
    if updates:
        db._client.table("value_bets").upsert(updates).execute()
        print(f"✅ Settled {len(updates)} bets!")

if __name__ == "__main__":
    settle_pending_bets()
