import streamlit as st
import pandas as pd
import sys
import os
from datetime import datetime

sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "..", "..")))
from src.bet_builder.config import get_supabase_client

st.set_page_config(page_title="Honest Ladder", page_icon="🪜", layout="wide")
st.title("🪜 The Honest Ladder Redesign")

# --- STATE MANAGEMENT ---
if "ladder_bankroll" not in st.session_state:
    st.session_state.ladder_bankroll = 5.0  # Starting with 5€
if "ladder_target" not in st.session_state:
    st.session_state.ladder_target = 50.0
if "current_step" not in st.session_state:
    st.session_state.current_step = 1
if "acc_legs" not in st.session_state:
    st.session_state.acc_legs = []

# --- TOP SECTION: Survival Dashboard ---
st.markdown("### 🛡️ Survival Dashboard")
col1, col2, col3, col4 = st.columns(4)
col1.metric("Current Bankroll", f"€{st.session_state.ladder_bankroll:.2f}")
col2.metric("Target Goal", f"€{st.session_state.ladder_target:.2f}")
col3.metric("Current Step", f"Step {st.session_state.current_step}")
col4.button("Reset Ladder 🔄", on_click=lambda: st.session_state.update(ladder_bankroll=5.0, current_step=1, acc_legs=[]))

st.divider()

# --- MIDDLE SECTION: Accumulator Builder ---
st.markdown("### 🏗️ Smart Accumulator Builder")
st.write("Select +EV legs to combine. The builder prevents same-game correlation.")

@st.cache_data(ttl=60)
def load_all_predictions():
    client = get_supabase_client()
    today_str = datetime.today().strftime('%Y-%m-%d')
    res = client.postgrest.from_("model_predictions").select("fixture_id, home_win_prob, draw_prob, away_win_prob, over_25_prob, over_15_prob, dc_1x_prob, dc_12_prob, dc_x2_prob, fixtures!inner(match_date, home_team_id, away_team_id, status)").eq("fixtures.status", "scheduled").gte("fixtures.match_date", today_str).execute()
    
    if not res.data:
        return []
        
    teams_res = client.table("teams").select("id, name").execute()
    teams = {t["id"]: t["name"] for t in teams_res.data}
    
    legs = []
    for row in res.data:
        fix = row.get("fixtures", {})
        fix_id = row["fixture_id"]
        home = teams.get(fix.get("home_team_id"), "Unknown")
        away = teams.get(fix.get("away_team_id"), "Unknown")
        match = f"{home} vs {away}"
        
        # Add Date String
        dt_str = "Unknown"
        dt = fix.get("match_date")
        if dt:
            dt_obj = datetime.fromisoformat(dt.replace("Z", "+00:00"))
            dt_str = dt_obj.strftime('%Y-%m-%d')
            match += f" ({dt_obj.strftime('%H:%M')})"
        
        # Add a few high-prob safe legs (e.g. Over 1.5, Double Chance)
        if row["over_15_prob"] > 0.75:
            legs.append({"Date": dt_str, "Fixture_ID": fix_id, "Match": match, "Market": "Over 1.5 Goals", "Prob": row["over_15_prob"]})
        if row["dc_1x_prob"] > 0.80:
            legs.append({"Date": dt_str, "Fixture_ID": fix_id, "Match": match, "Market": "1X (Home or Draw)", "Prob": row["dc_1x_prob"]})
        if row["dc_x2_prob"] > 0.80:
            legs.append({"Date": dt_str, "Fixture_ID": fix_id, "Match": match, "Market": "X2 (Away or Draw)", "Prob": row["dc_x2_prob"]})
            
    return sorted(legs, key=lambda x: x["Prob"], reverse=True)

all_legs = load_all_predictions()

col_left, col_right = st.columns([1.5, 1])

with col_left:
    st.markdown("##### Available High-Probability Safe Legs (>75%)")
    
    if all_legs:
        # Date Filter
        available_dates = sorted(list(set(leg["Date"] for leg in all_legs)))
        selected_date = st.selectbox("Filter by Date", ["All"] + available_dates)
        
        filtered_legs = all_legs if selected_date == "All" else [leg for leg in all_legs if leg["Date"] == selected_date]
        
        for i, leg in enumerate(filtered_legs):
            # We now allow same-game multis (Bet Builders)
            is_already_added = any(l["Fixture_ID"] == leg["Fixture_ID"] and l["Market"] == leg["Market"] for l in st.session_state.acc_legs)
            
            disabled = is_already_added
            button_label = "Added" if disabled else "Add to Acca ➕"
            
            c1, c2, c3 = st.columns([4, 1, 1])
            with c1:
                st.write(f"⚽ {leg['Match']} - {leg['Market']}")
            with c2:
                st.write(f"🎯 {leg['Prob']*100:.1f}%")
            with c3:
                if st.button(button_label, key=f"add_{i}_{leg['Fixture_ID']}_{leg['Market']}", disabled=disabled):
                    st.session_state.acc_legs.append({
                        "Fixture_ID": leg["Fixture_ID"],
                        "Match": leg["Match"],
                        "Market": leg["Market"],
                        "Prob": leg["Prob"]
                    })
                    st.rerun()
    else:
        st.warning("No high-probability safe legs available. Fetch odds from the API!")

with col_right:
    st.markdown("##### 🎫 Your Slip")
    
    if not st.session_state.acc_legs:
        st.info("Your slip is empty.")
    else:
        # Check for same-game correlation
        fixture_ids = [leg["Fixture_ID"] for leg in st.session_state.acc_legs]
        has_correlation = len(fixture_ids) != len(set(fixture_ids))
        
        if has_correlation:
            st.warning("⚠️ **Same-Game Multi Detected!** Multiplying probabilities for events in the same match is mathematically flawed because they are correlated (e.g. 1X and Over 1.5). The True Probability shown below is an estimate.")
        combined_prob = 1.0
        for i, leg in enumerate(st.session_state.acc_legs):
            combined_prob *= leg["Prob"]
            st.markdown(f"- **{leg['Match']}**\n  _{leg['Market']}_")
            if st.button("Remove ❌", key=f"rem_{i}"):
                st.session_state.acc_legs.pop(i)
                st.rerun()
                
        st.divider()
        st.metric("Combined True Probability", f"{(combined_prob*100):.1f}%")
        st.metric("Fair Odds (No Vig)", f"{1/combined_prob:.2f}")
        
        custom_odds = st.number_input("Enter Bookmaker Acca Odds:", min_value=1.0, value=max(1.01, round(1/combined_prob, 2)), step=0.05)
        
        ev = (combined_prob * custom_odds) - 1
        if ev > 0:
            st.success(f"✅ +EV Acca! Edge: {(ev*100):.1f}%")
        else:
            st.error(f"❌ -EV Acca. Expected Loss: {(ev*100):.1f}%")
            
        if st.button("Win Step & Update Bankroll 🎉", use_container_width=True):
            st.session_state.ladder_bankroll *= custom_odds
            st.session_state.current_step += 1
            st.session_state.acc_legs = [] # Clear slip for next step
            st.rerun()
