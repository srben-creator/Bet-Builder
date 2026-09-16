namespace BetBuilder.Domain.Entities;

public class LadderChallenge
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int ChallengeNumber { get; set; }
    public decimal InitialStake { get; set; } = 5.00m;
    public decimal TargetAmount { get; set; } = 50.00m;
    public decimal CurrentBankroll { get; set; } = 5.00m;
    public int CurrentStep { get; set; } = 1;
    public string Status { get; set; } = "active"; // active, completed, failed
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    public ICollection<LadderStep> Steps { get; set; } = new List<LadderStep>();
}
