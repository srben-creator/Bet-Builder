"""
Central configuration — loads env vars and creates the Supabase client.
Every module imports the client from here. Single source of truth.
"""
import os
from dotenv import load_dotenv
from supabase import create_client, Client

load_dotenv()

# We use .get() here to avoid crashing immediately if the .env isn't set up yet,
# but we'll still error out if they are missing when we try to connect.
SUPABASE_URL: str = os.environ.get("SUPABASE_URL", "")
SUPABASE_KEY: str = os.environ.get("SUPABASE_KEY", "")
ODDS_API_KEY: str = os.environ.get("ODDS_API_KEY", "")

# Minimum EV threshold to flag a value bet (in percent)
MIN_EV_THRESHOLD: float = 3.0

# Supported leagues with source-specific identifiers
LEAGUES = {
    "EPL":        {"name": "English Premier League",    "country": "England",  "fd_csv_code": "E0",  "understat_name": "EPL"},
    "La_Liga":    {"name": "La Liga",                   "country": "Spain",    "fd_csv_code": "SP1", "understat_name": "La_Liga"},
    "Bundesliga": {"name": "Bundesliga",                "country": "Germany",  "fd_csv_code": "D1",  "understat_name": "Bundesliga"},
    "Serie_A":    {"name": "Serie A",                   "country": "Italy",    "fd_csv_code": "I1",  "understat_name": "Serie_A"},
    "Ligue_1":    {"name": "Ligue 1",                   "country": "France",   "fd_csv_code": "F1",  "understat_name": "Ligue_1"},
    "Primeira":   {"name": "Primeira Liga",             "country": "Portugal", "fd_csv_code": "P1",  "understat_name": None},  # No Understat coverage
}

def get_supabase_client() -> Client:
    """Create and return a Supabase client. Call once, reuse everywhere."""
    if not SUPABASE_URL or not SUPABASE_KEY:
        raise ValueError("Missing SUPABASE_URL or SUPABASE_KEY in .env file.")
    return create_client(SUPABASE_URL, SUPABASE_KEY)
