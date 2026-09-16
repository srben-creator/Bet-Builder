namespace BetBuilder.Domain.Entities;

public class Odds
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FixtureId { get; set; }
    public Guid BookmakerId { get; set; }
    public string Market { get; set; } = "1X2"; // 1X2, OU25, OU15, DC, BTTS, AH
    public string Selection { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string OddsType { get; set; } = "closing"; // opening, closing, current
    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;

    public Fixture Fixture { get; set; } = null!;
    public Bookmaker Bookmaker { get; set; } = null!;
}
