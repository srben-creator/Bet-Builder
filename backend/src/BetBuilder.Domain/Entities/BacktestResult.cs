namespace BetBuilder.Domain.Entities;

public class BacktestResult
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FixtureId { get; set; }
    public string Market { get; set; } = string.Empty;
    public string Selection { get; set; } = string.Empty;
    public decimal ModelProb { get; set; }
    public decimal EvPct { get; set; }
    public decimal KellyFraction { get; set; }
    public Guid BookmakerId { get; set; }
    public decimal OddsPrice { get; set; }
    public decimal? PinnacleClosing { get; set; }
    public decimal? ClvPct { get; set; }
    public bool ResultWon { get; set; }
    public decimal Pnl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Fixture Fixture { get; set; } = null!;
    public Bookmaker Bookmaker { get; set; } = null!;
}
