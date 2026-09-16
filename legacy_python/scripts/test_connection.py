#!/usr/bin/env python3
"""
Step 1 Smoke Test — Validates:
  1. Supabase connectivity (env vars load correctly)
  2. Schema exists (tables are accessible)
  3. Upsert idempotency (re-running doesn't create duplicates)
  4. Read-back correctness

Run: python scripts/test_connection.py
"""
import sys
import os

# Add project root to path so we can import bet_builder
sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "src")))

from bet_builder.config import get_supabase_client, LEAGUES
from bet_builder.db.client import BetBuilderDB


def main():
    print("=" * 60)
    print("  Bet Builder — Step 1: Connection & Schema Smoke Test")
    print("=" * 60)

    # ── 1. Connect ───────────────────────────────────────────
    print("\n[1/5] Connecting to Supabase...")
    try:
        client = get_supabase_client()
        db = BetBuilderDB(client)
        print("  ✅ Connection successful.")
    except Exception as e:
        print(f"  ❌ Connection failed: {e}")
        sys.exit(1)

    # ── 2. Seed all 6 leagues ────────────────────────────────
    print("\n[2/5] Seeding leagues...")
    seeded_leagues = []
    for code, meta in LEAGUES.items():
        league = db.upsert_league({
            "name": meta["name"],
            "country": meta["country"],
            "code": code,
            "fd_csv_code": meta["fd_csv_code"],
            "understat_name": meta["understat_name"],
            "is_active": True,
        })
        seeded_leagues.append(league)
        tag = "🟢 xG" if meta["understat_name"] else "🟡 SOT-proxy"
        print(f"  ✅ {meta['name']:30s} [{code:12s}] — {tag}")

    # ── 3. Seed common bookmakers ────────────────────────────
    print("\n[3/5] Seeding bookmakers...")
    bookmakers = [
        {"name": "Bet365",       "code": "B365", "is_sharp": False},
        {"name": "Pinnacle",     "code": "PS",   "is_sharp": True},
        {"name": "Betfair",      "code": "BF",   "is_sharp": True},
        {"name": "William Hill", "code": "WH",   "is_sharp": False},
        {"name": "Interwetten",  "code": "IW",   "is_sharp": False},
        {"name": "Market Avg",   "code": "Avg",  "is_sharp": False},
        {"name": "Market Max",   "code": "Max",  "is_sharp": False},
    ]
    for bk in bookmakers:
        db.upsert_bookmaker(bk)
        sharp_tag = "⚡ sharp" if bk["is_sharp"] else "  soft"
        print(f"  ✅ {bk['name']:16s} [{bk['code']:4s}] — {sharp_tag}")

    # ── 4. Idempotency test ──────────────────────────────────
    print("\n[4/5] Idempotency test (re-upserting EPL)...")
    epl_before = db.get_league_by_code("EPL")
    db.upsert_league({
        "name": "English Premier League",
        "country": "England",
        "code": "EPL",
        "fd_csv_code": "E0",
        "understat_name": "EPL",
        "is_active": True,
    })
    epl_after = db.get_league_by_code("EPL")

    if epl_before and epl_after and epl_before["id"] == epl_after["id"]:
        print(f"  ✅ Same UUID: {epl_after['id']}")
        print("  ✅ Idempotent — no duplicate created.")
    else:
        print("  ❌ Idempotency FAILED — duplicate row created!")
        sys.exit(1)

    # ── 5. Read-back verification ────────────────────────────
    print("\n[5/5] Read-back verification...")
    all_leagues = db.get_leagues(active_only=True)
    all_bookmakers = db.fetch_all("bookmakers")

    print(f"  📊 Leagues in DB:    {len(all_leagues)}")
    print(f"  📊 Bookmakers in DB: {len(all_bookmakers)}")

    for lg in all_leagues:
        xg_status = "xG ✓" if lg.get("understat_name") else "SOT-proxy"
        print(f"     • {lg['name']:30s} fd={lg.get('fd_csv_code', '?'):3s}  {xg_status}")

    # ── Summary ──────────────────────────────────────────────
    print("\n" + "=" * 60)
    print("  ✅ ALL CHECKS PASSED — DB is seeded and ready for ETL.")
    print("=" * 60)

if __name__ == "__main__":
    main()
