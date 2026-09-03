-- ==============================================================================
-- +EV Football Betting System — Full Database Schema (Steps 1 & 2)
-- Run this entire script in your Supabase SQL Editor.
-- ==============================================================================

-- 1. Core Metadata Tables ------------------------------------------------------

CREATE TABLE IF NOT EXISTS leagues (
    id          UUID DEFAULT gen_random_uuid() PRIMARY KEY,
    name        TEXT NOT NULL,
    country     TEXT NOT NULL,
    code        TEXT NOT NULL UNIQUE,
    fd_csv_code TEXT,
    understat_name TEXT,
    is_active   BOOLEAN DEFAULT TRUE,
    created_at  TIMESTAMPTZ DEFAULT NOW(),
    updated_at  TIMESTAMPTZ DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS seasons (
    id          UUID DEFAULT gen_random_uuid() PRIMARY KEY,
    league_id   UUID NOT NULL REFERENCES leagues(id) ON DELETE CASCADE,
    start_year  INT NOT NULL,
    end_year    INT NOT NULL,
    is_current  BOOLEAN DEFAULT FALSE,
    created_at  TIMESTAMPTZ DEFAULT NOW(),
    UNIQUE(league_id, start_year)
);

CREATE TABLE IF NOT EXISTS teams (
    id              UUID DEFAULT gen_random_uuid() PRIMARY KEY,
    name            TEXT NOT NULL,
    short_name      TEXT,
    country         TEXT NOT NULL,
    fd_name         TEXT,
    understat_name  TEXT,
    created_at      TIMESTAMPTZ DEFAULT NOW(),
    updated_at      TIMESTAMPTZ DEFAULT NOW(),
    UNIQUE(name, country)
);

CREATE TABLE IF NOT EXISTS bookmakers (
    id          UUID DEFAULT gen_random_uuid() PRIMARY KEY,
    name        TEXT NOT NULL UNIQUE,
    code        TEXT NOT NULL UNIQUE,
    is_sharp    BOOLEAN DEFAULT FALSE,
    created_at  TIMESTAMPTZ DEFAULT NOW()
);


-- 2. Match Data Tables ---------------------------------------------------------

CREATE TABLE IF NOT EXISTS fixtures (
    id              UUID DEFAULT gen_random_uuid() PRIMARY KEY,
    season_id       UUID NOT NULL REFERENCES seasons(id) ON DELETE CASCADE,
    home_team_id    UUID NOT NULL REFERENCES teams(id),
    away_team_id    UUID NOT NULL REFERENCES teams(id),
    match_date      DATE NOT NULL,
    kick_off        TIME,
    status          TEXT DEFAULT 'scheduled'
                        CHECK (status IN ('scheduled','completed','postponed','cancelled')),
    
    -- Results
    home_goals      INT,
    away_goals      INT,
    result          TEXT CHECK (result IN ('H','D','A')),
    
    -- Stats (Added in Step 2/3)
    home_corners    INT,
    away_corners    INT,
    home_sot        INT,
    away_sot        INT,
    
    -- xG Data
    home_xg         NUMERIC(5,2),
    away_xg         NUMERIC(5,2),
    xg_source       TEXT CHECK (xg_source IN ('understat','statsbomb','manual')),
    
    created_at      TIMESTAMPTZ DEFAULT NOW(),
    updated_at      TIMESTAMPTZ DEFAULT NOW(),
    UNIQUE(home_team_id, away_team_id, match_date),
    CHECK (home_team_id != away_team_id)
);

CREATE INDEX IF NOT EXISTS idx_fixtures_season ON fixtures(season_id);
CREATE INDEX IF NOT EXISTS idx_fixtures_date ON fixtures(match_date DESC);
CREATE INDEX IF NOT EXISTS idx_fixtures_status ON fixtures(status);

CREATE TABLE IF NOT EXISTS odds (
    id              UUID DEFAULT gen_random_uuid() PRIMARY KEY,
    fixture_id      UUID NOT NULL REFERENCES fixtures(id) ON DELETE CASCADE,
    bookmaker_id    UUID NOT NULL REFERENCES bookmakers(id),
    market          TEXT NOT NULL DEFAULT '1X2'
                        CHECK (market IN ('1X2','OU25','OU15','DC','BTTS','AH')),
    selection       TEXT NOT NULL,
    price           NUMERIC(8,3) NOT NULL CHECK (price > 1.0),
    odds_type       TEXT DEFAULT 'closing'
                        CHECK (odds_type IN ('opening','closing','current')),
    captured_at     TIMESTAMPTZ DEFAULT NOW(),
    UNIQUE(fixture_id, bookmaker_id, market, selection, odds_type)
);

CREATE INDEX IF NOT EXISTS idx_odds_fixture ON odds(fixture_id);
CREATE INDEX IF NOT EXISTS idx_odds_market ON odds(market, selection);


-- 3. Model Output & Value Bets -------------------------------------------------

CREATE TABLE IF NOT EXISTS model_predictions (
    id                      UUID DEFAULT gen_random_uuid() PRIMARY KEY,
    fixture_id              UUID NOT NULL REFERENCES fixtures(id) ON DELETE CASCADE,
    model_version           TEXT NOT NULL DEFAULT 'v1',
    
    -- 1X2 Probabilities
    home_win_prob           NUMERIC(6,4) NOT NULL CHECK (home_win_prob BETWEEN 0 AND 1),
    draw_prob               NUMERIC(6,4) NOT NULL CHECK (draw_prob BETWEEN 0 AND 1),
    away_win_prob           NUMERIC(6,4) NOT NULL CHECK (away_win_prob BETWEEN 0 AND 1),
    
    -- Derived Probabilities (Added in Step 2)
    dc_1x_prob              NUMERIC(6,4),
    dc_12_prob              NUMERIC(6,4),
    dc_x2_prob              NUMERIC(6,4),
    over_15_prob            NUMERIC(6,4),
    over_25_prob            NUMERIC(6,4),
    
    -- Expected Goals
    predicted_home_goals    NUMERIC(5,2),
    predicted_away_goals    NUMERIC(5,2),
    
    created_at              TIMESTAMPTZ DEFAULT NOW(),
    UNIQUE(fixture_id, model_version)
);

CREATE TABLE IF NOT EXISTS value_bets (
    id                  UUID DEFAULT gen_random_uuid() PRIMARY KEY,
    fixture_id          UUID NOT NULL REFERENCES fixtures(id),
    prediction_id       UUID NOT NULL REFERENCES model_predictions(id),
    bookmaker_id        UUID NOT NULL REFERENCES bookmakers(id),
    market              TEXT NOT NULL,
    selection           TEXT NOT NULL,
    model_prob          NUMERIC(6,4) NOT NULL,
    odds_price          NUMERIC(8,3) NOT NULL,
    implied_prob        NUMERIC(6,4) NOT NULL,
    edge_pct            NUMERIC(6,2) NOT NULL,
    ev_pct              NUMERIC(8,2) NOT NULL,
    kelly_fraction      NUMERIC(6,4),
    recommended_stake   NUMERIC(6,4),
    status              TEXT DEFAULT 'pending'
                            CHECK (status IN ('pending','placed','won','lost','void','skipped')),
    actual_result       TEXT,
    pnl                 NUMERIC(10,2),
    created_at          TIMESTAMPTZ DEFAULT NOW(),
    settled_at          TIMESTAMPTZ,
    UNIQUE(fixture_id, bookmaker_id, market, selection)
);

CREATE INDEX IF NOT EXISTS idx_value_bets_status ON value_bets(status);
CREATE INDEX IF NOT EXISTS idx_value_bets_created ON value_bets(created_at DESC);


-- 4. Ladder Challenge Tables (Added in Step 2) ---------------------------------

CREATE TABLE IF NOT EXISTS ladder_challenges (
    id              UUID DEFAULT gen_random_uuid() PRIMARY KEY,
    challenge_number SERIAL UNIQUE,
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
    stake               NUMERIC(10,2) NOT NULL,
    combined_odds       NUMERIC(8,3) NOT NULL,
    compound_ev_pct     NUMERIC(8,2) NOT NULL,
    compound_win_prob   NUMERIC(6,4) NOT NULL,
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
    odds_price      NUMERIC(8,3) NOT NULL,
    ev_pct          NUMERIC(8,2) NOT NULL,
    result          TEXT CHECK (result IN ('won','lost','void','pending')),
    UNIQUE(step_id, fixture_id, market, selection)
);

CREATE INDEX IF NOT EXISTS idx_ladder_steps_challenge ON ladder_steps(challenge_id);
CREATE INDEX IF NOT EXISTS idx_ladder_legs_step ON ladder_legs(step_id);


-- 5. Auto-Update Triggers ------------------------------------------------------

CREATE OR REPLACE FUNCTION update_updated_at()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- We drop triggers first to make this script fully rerunnable
DROP TRIGGER IF EXISTS trg_leagues_updated ON leagues;
CREATE TRIGGER trg_leagues_updated BEFORE UPDATE ON leagues
    FOR EACH ROW EXECUTE FUNCTION update_updated_at();

DROP TRIGGER IF EXISTS trg_teams_updated ON teams;
CREATE TRIGGER trg_teams_updated BEFORE UPDATE ON teams
    FOR EACH ROW EXECUTE FUNCTION update_updated_at();

DROP TRIGGER IF EXISTS trg_fixtures_updated ON fixtures;
CREATE TRIGGER trg_fixtures_updated BEFORE UPDATE ON fixtures
    FOR EACH ROW EXECUTE FUNCTION update_updated_at();
