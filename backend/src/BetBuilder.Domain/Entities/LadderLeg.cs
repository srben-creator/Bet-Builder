namespace BetBuilder.Domain.Entities;

public class LadderLeg
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StepId { get; set; }
    public Guid FixtureId { get; set; }
    public string Market { get; set; } = string.Empty;
    public string Selection { get; set; } = string.Empty;
    public decimal ModelProb { get; set; }
    public decimal OddsPrice { get; set; }
    public decimal EvPct { get; set; }
    public string? Result { get; set; } // won, lost, void, pending

    public LadderStep Step { get; set; } = null!;
    public Fixture Fixture { get; set; } = null!;
}
