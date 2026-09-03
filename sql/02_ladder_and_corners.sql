-- Step 2 / 5: Schema Updates for Ladder Challenge and Corners

-- 1. Add Corner Columns to fixtures table
ALTER TABLE fixtures ADD COLUMN IF NOT EXISTS home_corners INT;
ALTER TABLE fixtures ADD COLUMN IF NOT EXISTS away_corners INT;

-- 2. Expand Odds Market Constraint (We must drop and recreate the constraint)
-- Since CHECK constraints are tricky to alter directly, we drop and re-add.
ALTER TABLE odds DROP CONSTRAINT IF EXISTS odds_market_check;
ALTER TABLE odds ADD CONSTRAINT odds_market_check 
    CHECK (market IN ('1X2','OU25','OU15','DC','BTTS','AH'));

-- 3. Expand model_predictions columns for derived probabilities
ALTER TABLE model_predictions ADD COLUMN IF NOT EXISTS dc_1x_prob NUMERIC(6,4);
ALTER TABLE model_predictions ADD COLUMN IF NOT EXISTS dc_12_prob NUMERIC(6,4);
ALTER TABLE model_predictions ADD COLUMN IF NOT EXISTS dc_x2_prob NUMERIC(6,4);
ALTER TABLE model_predictions ADD COLUMN IF NOT EXISTS over_15_prob NUMERIC(6,4);
ALTER TABLE model_predictions ADD COLUMN IF NOT EXISTS over_25_prob NUMERIC(6,4);

-- 4. Create Ladder Challenge Tables

CREATE TABLE IF NOT EXISTS ladder_challenges (
    id              UUID DEFAULT gen_random_uuid() PRIMARY KEY,
    challenge_number SERIAL UNIQUE,                  -- Auto-incrementing challenge #
    initial_stake   NUMERIC(10,2) NOT NULL DEFAULT 5.00,
    target_amount   NUMERIC(10,2) NOT NULL DEFAULT 50.00,
    current_bankroll NUMERIC(10,2) NOT NULL DEFAULT 5.00,
    current_step    INT NOT NULL DEFAULT 1,
    status          TEXT DEFAULT 'active'
                        CHECK (status IN ('active','completed','failed')),
    created_at      TIMESTAMPTZ DEFAULT NOW(),
    completed_at    TIMESTAMPTZ
);

CREATE TABLE IF NOT EXISTS ladder_steps (
    id                  UUID DEFAULT gen_random_uuid() PRIMARY KEY,
    challenge_id        UUID NOT NULL REFERENCES ladder_challenges(id) ON DELETE CASCADE,
    step_number         INT NOT NULL,
    stake               NUMERIC(10,2) NOT NULL,      -- Amount wagered this step
    combined_odds       NUMERIC(8,3) NOT NULL,       -- Accumulator combined decimal odds
    compound_ev_pct     NUMERIC(8,2) NOT NULL,       -- Compound EV of this accumulator
    compound_win_prob   NUMERIC(6,4) NOT NULL,       -- Model's compound win probability
    status              TEXT DEFAULT 'pending'
                            CHECK (status IN ('pending','won','lost','void')),
    pnl                 NUMERIC(10,2),
    settled_at          TIMESTAMPTZ,
    created_at          TIMESTAMPTZ DEFAULT NOW(),
    UNIQUE(challenge_id, step_number)
);

CREATE TABLE IF NOT EXISTS ladder_legs (
    id              UUID DEFAULT gen_random_uuid() PRIMARY KEY,
    step_id         UUID NOT NULL REFERENCES ladder_steps(id) ON DELETE CASCADE,
    fixture_id      UUID NOT NULL REFERENCES fixtures(id),
    market          TEXT NOT NULL,
    selection       TEXT NOT NULL,
    model_prob      NUMERIC(6,4) NOT NULL,
    odds_price      NUMERIC(8,3) NOT NULL,           -- Actual odds (user-supplied or from data)
    ev_pct          NUMERIC(8,2) NOT NULL,
    result          TEXT CHECK (result IN ('won','lost','void','pending')),
    UNIQUE(step_id, fixture_id, market, selection)
);

-- Index for quick lookups on ladder steps
CREATE INDEX IF NOT EXISTS idx_ladder_steps_challenge ON ladder_steps(challenge_id);
CREATE INDEX IF NOT EXISTS idx_ladder_legs_step ON ladder_legs(step_id);
