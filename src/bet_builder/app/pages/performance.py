import streamlit as st
import pandas as pd
import altair as alt
import sys
import os

# Ensure bet_builder is in the path
sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "..")))

from bet_builder.config import get_supabase_client

st.set_page_config(page_title="Performance | Bet Builder", layout="wide")

st.title("📈 Performance & Bankroll Evolution")
st.markdown("Track the real-world performance of your +EV betting strategy.")

import subprocess

st.info("💡 **Weekly Reminder**: Click the button below every Monday to fetch the weekend results and update your P&L!")
if st.button("🔄 Sync Weekend Data"):
    with st.spinner("Fetching weekend results... this may take a minute."):
        subprocess.run(["python", "src/bet_builder/etl/football_data.py"], cwd=os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "..", "..")))
    with st.spinner("Settling pending bets..."):
        subprocess.run(["python", "src/bet_builder/etl/settle_bets.py"], cwd=os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "..", "..")))
    st.success("Data synced successfully! Refreshing dashboard...")
    st.rerun()

client = get_supabase_client()

# Fetch settled bets
res = (
    client.from_("value_bets")
    .select("id, status, pnl, recommended_stake, created_at, market, odds_price")
    .in_("status", ["won", "lost"])
    .order("created_at", desc=False)
    .execute()
)

if not res.data:
    st.info("No settled bets found yet. Run the auto-settlement script after matches complete!")
    st.stop()
    
df = pd.DataFrame(res.data)

# Calculate KPIs
total_bets = len(df)
won_bets = len(df[df["status"] == "won"])
win_rate = (won_bets / total_bets) * 100

total_staked = df["recommended_stake"].sum()
total_profit = df["pnl"].sum()
roi = (total_profit / total_staked) * 100 if total_staked > 0 else 0

# Display KPI Cards
col1, col2, col3, col4 = st.columns(4)
col1.metric("Total Settled Bets", total_bets)
col2.metric("Win Rate", f"{win_rate:.1f}%")
col3.metric("Total Profit/Loss", f"{total_profit:+.2f} Units")
col4.metric("Overall ROI", f"{roi:+.1f}%")

st.divider()

# Bankroll Evolution Chart
st.subheader("Bankroll Evolution")
df["cumulative_pnl"] = df["pnl"].cumsum()
df["Date"] = pd.to_datetime(df["created_at"]).dt.tz_convert(None)

chart = alt.Chart(df).mark_line(color="#00C853", strokeWidth=3).encode(
    x=alt.X("Date:T", title="Date"),
    y=alt.Y("cumulative_pnl:Q", title="Cumulative P&L (Units)"),
    tooltip=[
        alt.Tooltip("Date:T", format="%Y-%m-%d %H:%M"),
        alt.Tooltip("market", title="Market"),
        alt.Tooltip("odds_price", title="Odds"),
        alt.Tooltip("pnl", title="P&L"),
        alt.Tooltip("cumulative_pnl", title="Bankroll")
    ]
).properties(height=400).interactive()

st.altair_chart(chart, use_container_width=True)

st.divider()

# Bet History Table
st.subheader("Settled Bet History")
display_df = df[["Date", "market", "status", "odds_price", "recommended_stake", "pnl"]].copy()
display_df.rename(columns={
    "market": "Market",
    "status": "Result",
    "odds_price": "Odds",
    "recommended_stake": "Stake",
    "pnl": "Profit/Loss"
}, inplace=True)

display_df.sort_values("Date", ascending=False, inplace=True)
st.dataframe(display_df, use_container_width=True, hide_index=True)
