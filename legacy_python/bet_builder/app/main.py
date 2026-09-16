import streamlit as st
import sys
import os

# Add root project path so we can import bet_builder
sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "..")))

st.set_page_config(
    page_title="+EV Football Bet Builder",
    page_icon="⚽",
    layout="wide",
    initial_sidebar_state="expanded"
)

st.title("⚽ +EV Football Bet Builder")
st.markdown("""
Welcome to your mathematical edge against the bookmakers.

**How it works:**
1. Our script fetches upcoming matches and odds from The Odds API.
2. The Dixon-Coles model predicts the exact probabilities of every outcome based on historical xG data.
3. We compare our "True Odds" against the Bookmaker Odds to find Expected Value (+EV).

👈 Select **Value Bets** from the sidebar to see single edges.
👈 Select **Honest Ladder** to build compounded low-risk accumulators.
""")

st.info("Run `python src/bet_builder/etl/odds_api.py` in your terminal to fetch the latest matches!")
