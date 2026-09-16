namespace BetBuilder.Domain.Entities;

public class LadderStep
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ChallengeId { get; set; }
    public int StepNumber { get; set; }
    public decimal Stake { get; set; }
    public decimal CombinedOdds { get; set; }
    public decimal CompoundEvPct { get; set; }
    public decimal CompoundWinProb { get; set; }
    public string Status { get; set; } = "pending"; // pending, won, lost, void
    public decimal? Pnl { get; set; }
    public DateTime? SettledAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public LadderChallenge Challenge { get; set; } = null!;
    public ICollection<LadderLeg> Legs { get; set; } = new List<LadderLeg>();
}
