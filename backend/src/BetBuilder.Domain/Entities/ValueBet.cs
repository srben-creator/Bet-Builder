namespace BetBuilder.Domain.Entities;

public class ValueBet
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FixtureId { get; set; }
    public Guid PredictionId { get; set; }
    public Guid BookmakerId { get; set; }
    public string Market { get; set; } = string.Empty;
    public string Selection { get; set; } = string.Empty;
    public decimal ModelProb { get; set; }
    public decimal OddsPrice { get; set; }
    public decimal? PinnaclePrice { get; set; }
    public decimal ImpliedProb { get; set; }
    public decimal EdgePct { get; set; }
    public decimal EvPct { get; set; }
    public decimal? KellyFraction { get; set; }
    public decimal? RecommendedStake { get; set; }
    public string Status { get; set; } = "pending"; // pending, placed, won, lost, void, skipped
    public string? ActualResult { get; set; }
    public decimal? Pnl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SettledAt { get; set; }

    public Fixture Fixture { get; set; } = null!;
    public ModelPrediction Prediction { get; set; } = null!;
    public Bookmaker Bookmaker { get; set; } = null!;
}
