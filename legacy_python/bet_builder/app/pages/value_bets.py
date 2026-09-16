import streamlit as st
import pandas as pd
import sys
import os
from datetime import datetime

sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "..", "..")))
from src.bet_builder.config import get_supabase_client

st.set_page_config(page_title="Value Bets", page_icon="💰", layout="wide")
st.title("💰 +EV Value Bets Tracker")

@st.cache_data(ttl=60)
def load_value_bets():
    client = get_supabase_client()
    
    # Complex join across value_bets, odds, fixtures, and teams
    query = """
    select 
        vb.ev_pct,
        vb.kelly_fraction,
        vb.model_prob,
        vb.odds_price,
        vb.market,
        vb.selection,
        f.match_date,
        ht.name as home_team,
        at.name as away_team
    from value_bets vb
    join fixtures f on vb.fixture_id = f.id
    join teams ht on f.home_team_id = ht.id
    join teams at on f.away_team_id = at.id
    where f.status = 'scheduled'
    order by vb.ev_pct desc
    """
    
    today_str = datetime.today().strftime('%Y-%m-%d')
    res = client.postgrest.from_("value_bets").select("ev_pct, kelly_fraction, model_prob, odds_price, market, selection, fixture_id, bookmaker_id, fixtures!inner(match_date, home_team_id, away_team_id, status, season_id)").eq("fixtures.status", "scheduled").gte("fixtures.match_date", today_str).order("ev_pct", desc=True).execute()
    
    if not res.data:
        return pd.DataFrame()
        
    teams_res = client.table("teams").select("id, name").execute()
    teams = {t["id"]: t["name"] for t in teams_res.data}
    
    leagues_res = client.table("leagues").select("id, name, country").execute()
    leagues_map = {l["id"]: f"{l['name']} ({l['country']})" for l in leagues_res.data}
    
    seasons_res = client.table("seasons").select("id, league_id").execute()
    seasons_map = {s["id"]: leagues_map.get(s["league_id"], "Unknown") for s in seasons_res.data}
    
    # Fetch Pinnacle Odds to display as the Sharp Line
    pinnacle_res = client.table("bookmakers").select("id").eq("code", "pinnacle").execute()
    pinnacle_odds_map = {}
    if pinnacle_res.data:
        pinnacle_id = pinnacle_res.data[0]["id"]
        
        # Get unique fixture IDs from the value bets to avoid hitting the 1000 row limit
        unique_fixture_ids = list(set([row.get("fixture_id") for row in res.data if row.get("fixture_id")]))
        
        # Fetch odds for those specific fixtures in batches of 50
        for i in range(0, len(unique_fixture_ids), 50):
            batch = unique_fixture_ids[i:i+50]
            if not batch: continue
            
            p_odds = client.table("odds").select("fixture_id, market, selection, price").eq("bookmaker_id", pinnacle_id).in_("fixture_id", batch).execute()
            for o in p_odds.data:
                pinnacle_odds_map[(o["fixture_id"], o["market"], o["selection"])] = o["price"]
            
    # Fetch all bookmakers to display the Soft Bookie Name instead of just "Bookie"
    bookies_res = client.table("bookmakers").select("id, name").execute()
    bookies_map = {b["id"]: b["name"] for b in bookies_res.data}
    
    rows = []
    for row in res.data:
        fix = row.get("fixtures", {})
        
        home_name = teams.get(fix.get("home_team_id"), "Unknown")
        away_name = teams.get(fix.get("away_team_id"), "Unknown")
        league_name = seasons_map.get(fix.get("season_id"), "Unknown")
        
        match = f"{home_name} vs {away_name}"
        
        dt_str = "Unknown"
        dt = fix.get("match_date")
        if dt:
            dt_obj = datetime.fromisoformat(dt.replace("Z", "+00:00"))
            match += f" ({dt_obj.strftime('%H:%M')})"
            dt_str = dt_obj.strftime('%Y-%m-%d')
            
        m = row.get("market")
        s = row.get("selection")
        bookie_id = row.get("bookmaker_id")
        
        pinnacle_price = pinnacle_odds_map.get((row.get("fixture_id"), m, s))
        pinnacle_str = f"{pinnacle_price:.2f}" if pinnacle_price else "N/A"
        bookie_name = bookies_map.get(bookie_id, "Bookie")
            
        rows.append({
            "Date": dt_str,
            "League": league_name,
            "Match": match,
            "Market": m,
            "Selection": s,
            "Prob %": f"{row.get('model_prob', 0) * 100:.1f}%",
            "True Odds": f"{1.0/row.get('model_prob', 1) if row.get('model_prob') else 0:.2f}",
            "Pinnacle": pinnacle_str,
            "Bookmaker": bookie_name,
            "Odds": f"{row.get('odds_price', 0):.2f}",
            "Edge (+EV)": float(row.get('ev_pct', 0)),
            "Q-Kelly Stake": f"{row.get('kelly_fraction', 0):.2f}%"
        })
        
    df = pd.DataFrame(rows)
    return df

df = load_value_bets()

if df.empty:
    st.warning("No value bets found for upcoming matches. Make sure you run the Odds API fetcher!")
else:
    # --- Sidebar Filters ---
    st.sidebar.header("Filter Value Bets")
    
    # Date Filter
    available_dates = sorted(df["Date"].unique())
    selected_dates = st.sidebar.multiselect("Select Dates", available_dates, default=available_dates)
    
    # League Filter
    available_leagues = sorted(df["League"].unique())
    selected_leagues = st.sidebar.multiselect("Select Leagues", available_leagues, default=available_leagues)
    
    # Edge Filter
    min_edge = float(df["Edge (+EV)"].min()) if not df.empty else 0.0
    max_edge = float(df["Edge (+EV)"].max()) if not df.empty else 100.0
    
    # Add a slider, but cap the max visual range so massive outliers don't break the slider UX
    slider_max = min(max_edge, 100.0) 
    edge_threshold = st.sidebar.slider("Minimum Edge %", min_value=0.0, max_value=slider_max, value=2.0, step=0.5)
    
    # Apply Filters
    df_filtered = df[(df["Date"].isin(selected_dates)) & (df["League"].isin(selected_leagues)) & (df["Edge (+EV)"] >= edge_threshold)].copy()
    
    # Format Edge as string AFTER filtering
    df_filtered["Edge (+EV)"] = df_filtered["Edge (+EV)"].apply(lambda x: f"{x:.2f}%")
    
    st.dataframe(df_filtered, use_container_width=True, hide_index=True)
    
    st.markdown("""
    ### 💡 How to read this table:
    - **Prob %**: The exact probability of this event occurring according to our Math Engine.
    - **True Odds**: What the odds *should* be based on math (1 / Prob).
    - **Pinnacle**: The odds offered by Pinnacle (the sharpest bookmaker in the world). Use this as your safety net!
    - **Bookmaker & Odds**: The soft bookmaker offering the value bet, and their price.
    - **Edge (+EV)**: Your statistical advantage over the bookmaker. 
    - **Q-Kelly Stake**: Recommended % of your total bankroll to bet on this (Quarter-Kelly strategy for safety).
    """)
