"""
Constants for the ETL pipeline.
Defines league metadata, historical seasons to ingest, and standard column mappings.
"""

import datetime

# Number of historical seasons to ingest (including current)
SEASONS_TO_INGEST = 5

# Dynamically determine the current season's start year (European calendar usually starts in August)
_now = datetime.datetime.now()
CURRENT_SEASON_START_YEAR = _now.year if _now.month >= 7 else _now.year - 1

# Mapping from our internal codes to football-data.co.uk CSV prefixes
FD_LEAGUES = {
    "EPL": "E0",
    "La_Liga": "SP1",
    "Bundesliga": "D1",
    "Serie_A": "I1",
    "Ligue_1": "F1",
    "Primeira": "P1"
}

# The base URL format for football-data.co.uk CSVs
# e.g., https://www.football-data.co.uk/mmz4281/2425/E0.csv
FD_BASE_URL = "https://www.football-data.co.uk/mmz4281"

# Standard columns we care about in the CSV
FD_COLUMNS = [
    "Div", "Date", "HomeTeam", "AwayTeam", 
    "FTHG", "FTAG", "FTR", # Goals and result
    "HS", "AS", "HST", "AST", # Shots (used for Portugal proxy)
    "HC", "AC" # Corners (saved for Step 6)
]

# Supported Bookmakers to extract from the CSV
# Using Closing odds (C) if available, otherwise opening/standard
FD_BOOKMAKERS = {
    "B365": {"H": "B365H", "D": "B365D", "A": "B365A"},
    "PS":   {"H": "PSH",   "D": "PSD",   "A": "PSA"},   # Pinnacle Opening
    "PSC":  {"H": "PSCH",  "D": "PSCD",  "A": "PSCA"},  # Pinnacle Closing
    "Max":  {"H": "MaxH",  "D": "MaxD",  "A": "MaxA"},
    "Avg":  {"H": "AvgH",  "D": "AvgD",  "A": "AvgA"},
}

# Over/Under 2.5 columns
FD_OU25_BOOKMAKERS = {
    "B365": {"Over": "B365>2.5", "Under": "B365<2.5"},
    "PS":   {"Over": "P>2.5",    "Under": "P<2.5"},
    "PSC":  {"Over": "PC>2.5",   "Under": "PC<2.5"},
    "Max":  {"Over": "Max>2.5",  "Under": "Max<2.5"},
    "Avg":  {"Over": "Avg>2.5",  "Under": "Avg<2.5"},
}
