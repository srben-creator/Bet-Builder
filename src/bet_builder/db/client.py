"""
Database helpers — thin wrappers around Supabase CRUD with idempotent upserts.
Each function targets one table and handles conflicts via UNIQUE constraints.
"""
from typing import Any
from supabase import Client

class BetBuilderDB:
    """Lightweight DB access layer. Wraps the Supabase client with
    table-specific helpers that enforce upsert-on-conflict semantics."""

    def __init__(self, client: Client):
        self._client = client

    # ── Leagues ──────────────────────────────────────────────────
    def upsert_league(self, league: dict[str, Any]) -> dict:
        """Upsert a league. Conflict key: 'code' (UNIQUE)."""
        result = (
            self._client.table("leagues")
            .upsert(league, on_conflict="code")
            .execute()
        )
        return result.data[0] if result.data else {}

    def get_leagues(self, active_only: bool = True) -> list[dict]:
        query = self._client.table("leagues").select("*")
        if active_only:
            query = query.eq("is_active", True)
        return query.execute().data

    def get_league_by_code(self, code: str) -> dict | None:
        result = (
            self._client.table("leagues")
            .select("*")
            .eq("code", code)
            .maybe_single()
            .execute()
        )
        return result.data

    # ── Teams ────────────────────────────────────────────────────
    def upsert_team(self, team: dict[str, Any]) -> dict:
        """Upsert a team. Conflict key: ('name', 'country')."""
        result = (
            self._client.table("teams")
            .upsert(team, on_conflict="name,country")
            .execute()
        )
        return result.data[0] if result.data else {}

    # ── Seasons ──────────────────────────────────────────────────
    def upsert_season(self, season: dict[str, Any]) -> dict:
        """Upsert a season. Conflict key: ('league_id', 'start_year')."""
        result = (
            self._client.table("seasons")
            .upsert(season, on_conflict="league_id,start_year")
            .execute()
        )
        return result.data[0] if result.data else {}

    # ── Fixtures ─────────────────────────────────────────────────
    def upsert_fixture(self, fixture: dict[str, Any]) -> dict:
        result = (
            self._client.table("fixtures")
            .upsert(fixture, on_conflict="home_team_id,away_team_id,match_date")
            .execute()
        )
        return result.data[0] if result.data else {}

    def upsert_fixtures_bulk(self, fixtures: list[dict[str, Any]]) -> list[dict]:
        if not fixtures: return []
        result = (
            self._client.table("fixtures")
            .upsert(fixtures, on_conflict="home_team_id,away_team_id,match_date")
            .execute()
        )
        return result.data

    # ── Bookmakers ───────────────────────────────────────────────
    def upsert_bookmaker(self, bookmaker: dict[str, Any]) -> dict:
        result = (
            self._client.table("bookmakers")
            .upsert(bookmaker, on_conflict="code")
            .execute()
        )
        return result.data[0] if result.data else {}

    # ── Odds ─────────────────────────────────────────────────────
    def upsert_odds(self, odds: dict[str, Any]) -> dict:
        result = (
            self._client.table("odds")
            .upsert(odds, on_conflict="fixture_id,bookmaker_id,market,selection,odds_type")
            .execute()
        )
        return result.data[0] if result.data else {}

    def upsert_odds_bulk(self, odds: list[dict[str, Any]]) -> list[dict]:
        if not odds: return []
        # Supabase API limits batch sizes, but a few hundred is fine.
        result = (
            self._client.table("odds")
            .upsert(odds, on_conflict="fixture_id,bookmaker_id,market,selection,odds_type")
            .execute()
        )
        return result.data

    # ── Generic ──────────────────────────────────────────────────
    def fetch_all(self, table: str) -> list[dict]:
        """Fetch all rows from any table. Use sparingly."""
        return self._client.table(table).select("*").execute().data
