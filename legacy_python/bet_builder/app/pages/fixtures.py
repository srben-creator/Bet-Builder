import streamlit as st
import pandas as pd
from datetime import datetime
import sys
import os

# Ensure bet_builder is in the path
sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "..")))

from bet_builder.config import get_supabase_client

st.set_page_config(page_title="Upcoming Fixtures | Bet Builder", layout="wide")

st.title("📅 Upcoming Fixtures")
st.markdown("Browse all scheduled matches fetched from the system.")

client = get_supabase_client()

# Fetch active leagues
leagues_res = client.table("leagues").select("id, name, country").eq("is_active", True).execute()
leagues_dict = {l["id"]: f"{l['name']} ({l['country']})" for l in leagues_res.data}

if not leagues_dict:
    st.warning("No active leagues found.")
    st.stop()

# Build team map
teams_res = client.table("teams").select("id, name").execute()
teams_dict = {t["id"]: t["name"] for t in teams_res.data}

# Filter by League
selected_leagues_names = st.sidebar.multiselect(
    "Filter by League",
    options=list(leagues_dict.values()),
    default=list(leagues_dict.values())
)
selected_league_ids = [lid for lid, lname in leagues_dict.items() if lname in selected_leagues_names]

if not selected_league_ids:
    st.info("Select at least one league to view fixtures.")
    st.stop()

# Fetch fixtures
today_str = datetime.today().strftime('%Y-%m-%d')
res = (
    client.from_("fixtures")
    .select("id, match_date, home_team_id, away_team_id, status, seasons!inner(league_id)")
    .eq("status", "scheduled")
    .gte("match_date", today_str)
    .in_("seasons.league_id", selected_league_ids)
    .order("match_date", desc=False)
    .execute()
)

if not res.data:
    st.info("No upcoming fixtures found for the selected leagues.")
    st.stop()

rows = []
for fix in res.data:
    home = teams_dict.get(fix["home_team_id"], "Unknown")
    away = teams_dict.get(fix["away_team_id"], "Unknown")
    league_name = leagues_dict.get(fix["seasons"]["league_id"], "Unknown")
    
    dt_str = fix["match_date"]
    if dt_str:
        dt_obj = datetime.fromisoformat(dt_str.replace("Z", "+00:00"))
        date_formatted = dt_obj.strftime("%Y-%m-%d")
        time_formatted = dt_obj.strftime("%H:%M")
    else:
        date_formatted = "Unknown"
        time_formatted = "Unknown"
        
    rows.append({
        "League": league_name,
        "Date": date_formatted,
        "Time (UTC)": time_formatted,
        "Home Team": home,
        "Away Team": away,
        "Status": fix["status"].title()
    })

df = pd.DataFrame(rows)

# Display grouped by date
dates = sorted(df["Date"].unique())

for d in dates:
    st.subheader(f"🗓️ {d}")
    df_date = df[df["Date"] == d].drop(columns=["Date"])
    st.dataframe(df_date, use_container_width=True, hide_index=True)
