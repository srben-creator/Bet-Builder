import pandas as pd
import requests
import io
import sys
import os
from datetime import datetime
from typing import Dict, Any

# Add src to python path so bet_builder can be imported
sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..")))

from bet_builder.config import get_supabase_client
from bet_builder.db.client import BetBuilderDB
from bet_builder.etl.constants import (
    SEASONS_TO_INGEST,
    CURRENT_SEASON_START_YEAR,
    FD_BASE_URL,
    FD_BOOKMAKERS,
    FD_OU25_BOOKMAKERS,
)
from bet_builder.etl.team_mapping import get_canonical_name


class FootballDataETL:
    def __init__(self, db: BetBuilderDB):
        self.db = db
        # Cache for teams and bookmakers to avoid redundant upserts/lookups
        self.teams_cache: Dict[str, str] = {}
        self.bookmakers_cache: Dict[str, str] = {}

    def run(self):
        """Main entry point to run the ETL for all active leagues."""
        print("Starting football-data.co.uk ETL...")
        leagues = self.db.get_leagues(active_only=True)
        
        # Load bookmakers cache
        bookmakers = self.db.fetch_all("bookmakers")
        for b in bookmakers:
            self.bookmakers_cache[b["code"]] = b["id"]

        for league in leagues:
            if not league.get("fd_csv_code"):
                print(f"Skipping {league['name']} (No FD CSV code)")
                continue
                
            print(f"\nProcessing League: {league['name']} ({league['fd_csv_code']})")
            
            # Start from (CURRENT_SEASON - SEASONS_TO_INGEST + 1)
            # E.g., if current is 2024, and we want 5 seasons, range is 2020 to 2024.
            start_year = CURRENT_SEASON_START_YEAR - SEASONS_TO_INGEST + 1
            for year in range(start_year, CURRENT_SEASON_START_YEAR + 1):
                self._process_season(league, year)

    def _process_season(self, league: dict, start_year: int):
        # Build the short year string, e.g. 2024 -> "2425"
        short_start = str(start_year)[-2:]
        short_end = str(start_year + 1)[-2:]
        season_str = f"{short_start}{short_end}"
        
        url = f"{FD_BASE_URL}/{season_str}/{league['fd_csv_code']}.csv"
        print(f"  Downloading season {start_year}/{start_year+1}: {url}")
        
        try:
            response = requests.get(url, timeout=10)
            if response.status_code == 404:
                print(f"    [!] Not found (might not exist yet): {url}")
                return
            response.raise_for_status()
        except requests.RequestException as e:
            print(f"    [!] Failed to download: {e}")
            return

        # Read CSV into DataFrame
        df = pd.read_csv(io.StringIO(response.text), on_bad_lines="skip")
        if df.empty or "Date" not in df.columns:
            print(f"    [!] Empty or invalid CSV.")
            return

        # Ensure Date column is parsed as string correctly before converting
        df.dropna(subset=["Date", "HomeTeam", "AwayTeam"], inplace=True)
        
        # Upsert the season
        season_record = self.db.upsert_season({
            "league_id": league["id"],
            "start_year": start_year,
            "end_year": start_year + 1,
            "is_current": (start_year == CURRENT_SEASON_START_YEAR)
        })
        season_id = season_record["id"]
        
        fixtures_added = 0
        
        odds_batch = []
        
        for idx, row in df.iterrows():
            try:
                # Parse Date (sometimes it's dd/mm/yy, sometimes dd/mm/yyyy)
                date_str = str(row["Date"]).strip()
                if len(date_str.split("/")[-1]) == 2:
                    match_date = datetime.strptime(date_str, "%d/%m/%y").date()
                else:
                    match_date = datetime.strptime(date_str, "%d/%m/%Y").date()
                    
                # Time is sometimes available
                kick_off = None
                if "Time" in row and pd.notna(row["Time"]):
                    kick_off = str(row["Time"]).strip()

                # Process Teams
                raw_home = str(row["HomeTeam"])
                raw_away = str(row["AwayTeam"])
                home_team_id = self._get_or_create_team(raw_home, league["country"])
                away_team_id = self._get_or_create_team(raw_away, league["country"])

                # Results & Stats
                fthg = int(row["FTHG"]) if pd.notna(row.get("FTHG")) else None
                ftag = int(row["FTAG"]) if pd.notna(row.get("FTAG")) else None
                result = str(row["FTR"]).strip() if pd.notna(row.get("FTR")) else None
                
                # Stats
                hc = int(row["HC"]) if "HC" in row and pd.notna(row["HC"]) else None
                ac = int(row["AC"]) if "AC" in row and pd.notna(row["AC"]) else None
                hst = int(row["HST"]) if "HST" in row and pd.notna(row["HST"]) else None
                ast = int(row["AST"]) if "AST" in row and pd.notna(row["AST"]) else None

                # Status is 'completed' if we have a result
                status = "completed" if result in ["H", "D", "A"] else "scheduled"

                fixture_data = {
                    "season_id": season_id,
                    "home_team_id": home_team_id,
                    "away_team_id": away_team_id,
                    "match_date": match_date.isoformat(),
                    "kick_off": kick_off,
                    "status": status,
                    "home_goals": fthg,
                    "away_goals": ftag,
                    "result": result,
                    "home_corners": hc,
                    "away_corners": ac,
                    "home_sot": hst,
                    "away_sot": ast,
                }
                
                fixture_record = self.db.upsert_fixture(fixture_data)
                fixture_id = fixture_record["id"]
                fixtures_added += 1

                if fixtures_added % 50 == 0:
                    print(f"      ... processed {fixtures_added} fixtures")

                # Process Odds
                row_odds = self._process_odds(fixture_id, row)
                odds_batch.extend(row_odds)
                
                # Bulk insert odds every 50 matches to avoid memory bloat and save API calls
                if len(odds_batch) > 500:
                    self.db.upsert_odds_bulk(odds_batch)
                    odds_batch = []
                
            except Exception as e:
                # Catch row-level errors so we don't fail the whole file
                print(f"    [!] Error parsing row {row.get('Date')} {row.get('HomeTeam')} vs {row.get('AwayTeam')}: {e}")
                continue
                
        # Insert any remaining odds
        if odds_batch:
            self.db.upsert_odds_bulk(odds_batch)
            
        print(f"    ✅ Completed {start_year}/{start_year+1}: {fixtures_added} fixtures inserted.")

    def _get_or_create_team(self, raw_name: str, country: str) -> str:
        """Normalizes team name, caches, and upserts if needed."""
        canonical_name = get_canonical_name(raw_name)
        cache_key = f"{canonical_name}_{country}"
        
        if cache_key in self.teams_cache:
            return self.teams_cache[cache_key]
            
        team_record = self.db.upsert_team({
            "name": canonical_name,
            "country": country,
            "fd_name": raw_name.strip()
        })
        self.teams_cache[cache_key] = team_record["id"]
        return team_record["id"]

    def _process_odds(self, fixture_id: str, row: pd.Series) -> list[dict]:
        """Extracts 1X2 and OU2.5 odds from the row and returns a list of dictionaries."""
        odds_list = []
        
        # 1. 1X2 Market
        for bk_code, cols in FD_BOOKMAKERS.items():
            if bk_code not in self.bookmakers_cache:
                continue
            bk_id = self.bookmakers_cache[bk_code]
            
            # Check if all 3 columns exist and are not NaN
            if all(col in row and pd.notna(row[col]) for col in cols.values()):
                # Determine odds_type (Closing if 'C' in code, else Opening)
                odds_type = "closing" if "C" in bk_code else "opening"
                
                for selection, col_name in cols.items():
                    price = float(row[col_name])
                    if price > 1.0:
                        odds_list.append({
                            "fixture_id": fixture_id,
                            "bookmaker_id": bk_id,
                            "market": "1X2",
                            "selection": selection,
                            "price": price,
                            "odds_type": odds_type
                        })

        # 2. OU 2.5 Market
        for bk_code, cols in FD_OU25_BOOKMAKERS.items():
            if bk_code not in self.bookmakers_cache:
                continue
            bk_id = self.bookmakers_cache[bk_code]
            
            if all(col in row and pd.notna(row[col]) for col in cols.values()):
                odds_type = "closing" if "C" in bk_code else "opening"
                
                for selection, col_name in cols.items():
                    price = float(row[col_name])
                    if price > 1.0:
                        odds_list.append({
                            "fixture_id": fixture_id,
                            "bookmaker_id": bk_id,
                            "market": "OU25",
                            "selection": selection,
                            "price": price,
                            "odds_type": odds_type
                        })
                        
        return odds_list

if __name__ == "__main__":
    client = get_supabase_client()
    db = BetBuilderDB(client)
    etl = FootballDataETL(db)
    etl.run()
