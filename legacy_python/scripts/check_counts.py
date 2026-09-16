import sys, os
sys.path.insert(0, os.path.abspath('src'))
from bet_builder.config import get_supabase_client

client = get_supabase_client()

print("--- Database Table Counts ---")
for table in ['leagues', 'bookmakers', 'seasons', 'teams', 'fixtures', 'odds']:
    try:
        res = client.table(table).select('id', count='exact').limit(1).execute()
        print(f"{table.ljust(15)}: {res.count} rows")
    except Exception as e:
        print(f"{table.ljust(15)}: Error - {e}")
