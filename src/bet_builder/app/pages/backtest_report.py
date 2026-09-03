import streamlit as st
import pandas as pd
import altair as alt
import sys
import os

# Ensure bet_builder is in the path
sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "..")))

from bet_builder.config import get_supabase_client

st.set_page_config(page_title="Backtest Report | Bet Builder", layout="wide")

st.title("🔬 Backtest Report: ROI & Closing Line Value")
st.markdown("Analyze the historical performance of the Math Engine.")

client = get_supabase_client()

# Fetch backtest results
res = (
    client.from_("backtest_results")
    .select("*, fixtures!inner(match_date, home_team_id, away_team_id, seasons!inner(league_id))")
    .order("created_at", desc=False)
    .execute()
)

if not res.data:
    st.warning("No backtest results found in the database. Run the backtester script first!")
    st.stop()
    
# Flatten data
flat_data = []
for row in res.data:
    flat = row.copy()
    fix = flat.pop("fixtures")
    flat["match_date"] = fix["match_date"]
    flat["league_id"] = fix["seasons"]["league_id"]
    flat_data.append(flat)

df = pd.DataFrame(flat_data)
df["match_date"] = pd.to_datetime(df["match_date"])
df = df.sort_values("match_date")

# --- Top Level KPIs ---
total_bets = len(df)
won_bets = len(df[df["result_won"] == True])
win_rate = (won_bets / total_bets) * 100 if total_bets > 0 else 0

total_staked = df["kelly_fraction"].sum()
total_profit = df["pnl"].sum()
roi = (total_profit / total_staked) * 100 if total_staked > 0 else 0

# CLV Metrics
df_clv = df.dropna(subset=["clv_pct"])
beat_closing_count = len(df_clv[df_clv["clv_pct"] > 0])
beat_closing_rate = (beat_closing_count / len(df_clv)) * 100 if len(df_clv) > 0 else 0
avg_clv = df_clv["clv_pct"].mean() if not df_clv.empty else 0

col1, col2, col3, col4, col5 = st.columns(5)
col1.metric("Total Simulated Bets", total_bets)
col2.metric("Win Rate", f"{win_rate:.1f}%")
col3.metric("Overall ROI", f"{roi:+.2f}%")
col4.metric("Beat Closing Line", f"{beat_closing_rate:.1f}%")
col5.metric("Avg CLV Edge", f"{avg_clv:+.2f}%")

st.divider()

# --- Cumulative ROI Chart ---
st.subheader("Cumulative P&L Evolution (Units)")
df["cumulative_pnl"] = df["pnl"].cumsum()

chart = alt.Chart(df).mark_line(color="#2962FF", strokeWidth=3).encode(
    x=alt.X("match_date:T", title="Date"),
    y=alt.Y("cumulative_pnl:Q", title="Cumulative Units (P&L)"),
    tooltip=["match_date", "cumulative_pnl", "market", "selection", "odds_price", "pnl"]
).properties(height=400).interactive()

st.altair_chart(chart, use_container_width=True)

st.divider()

# --- Performance by Market ---
st.subheader("Performance by Market")
market_stats = df.groupby("market").agg(
    Bets=("id", "count"),
    WinRate=("result_won", lambda x: (x.sum() / len(x)) * 100),
    TotalStaked=("kelly_fraction", "sum"),
    TotalProfit=("pnl", "sum")
).reset_index()

market_stats["ROI (%)"] = (market_stats["TotalProfit"] / market_stats["TotalStaked"]) * 100

st.dataframe(
    market_stats.style.format({
        "WinRate": "{:.1f}%",
        "TotalStaked": "{:.2f}",
        "TotalProfit": "{:+.2f}",
        "ROI (%)": "{:+.2f}%"
    }), 
    use_container_width=True
)

# --- CLV Distribution ---
st.subheader("Closing Line Value (CLV) Distribution")
st.markdown("Shows how often our Opening Odds beat Pinnacle's Closing Odds. Values > 0 mean we beat the market.")
if not df_clv.empty:
    clv_chart = alt.Chart(df_clv).mark_bar(opacity=0.7).encode(
        x=alt.X("clv_pct:Q", bin=alt.Bin(maxbins=30), title="CLV Edge %"),
        y=alt.Y("count()", title="Number of Bets"),
        color=alt.condition(
            alt.datum.clv_pct > 0,
            alt.value("#00C853"),  # Green if > 0
            alt.value("#D50000")   # Red if <= 0
        )
    ).properties(height=300)
    st.altair_chart(clv_chart, use_container_width=True)
else:
    st.info("No CLV data available. (Pinnacle closing odds missing for these fixtures)")
