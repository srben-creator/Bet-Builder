-- Step 3: Add Shots on Target to Fixtures

ALTER TABLE fixtures ADD COLUMN IF NOT EXISTS home_sot INT;
ALTER TABLE fixtures ADD COLUMN IF NOT EXISTS away_sot INT;
