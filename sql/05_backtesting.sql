-- 05_backtesting.sql

CREATE TABLE IF NOT EXISTS backtest_results (
    id                  UUID DEFAULT gen_random_uuid() PRIMARY KEY,
    fixture_id          UUID NOT NULL REFERENCES fixtures(id) ON DELETE CASCADE,
    market              TEXT NOT NULL,
    selection           TEXT NOT NULL,
    
    -- Model metrics at time of prediction
    model_prob          NUMERIC(8,4) NOT NULL,
    ev_pct              NUMERIC(8,4) NOT NULL,
    kelly_fraction      NUMERIC(8,4) NOT NULL,
    
    -- Odds at time of placement
    bookmaker_id        UUID NOT NULL REFERENCES bookmakers(id),
    odds_price          NUMERIC(8,3) NOT NULL,
    
    -- Closing Line Value (CLV)
    pinnacle_closing    NUMERIC(8,3),
    clv_pct             NUMERIC(8,4), -- Edge over the closing line
    
    -- Result and Bankroll impact
    result_won          BOOLEAN NOT NULL,
    pnl                 NUMERIC(10,4) NOT NULL,
    
    created_at          TIMESTAMPTZ DEFAULT NOW(),
    UNIQUE(fixture_id, market, selection)
);

CREATE INDEX IF NOT EXISTS idx_backtest_fixture ON backtest_results(fixture_id);
