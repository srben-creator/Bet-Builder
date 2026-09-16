namespace BetBuilder.Domain.Entities;

public class Fixture
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SeasonId { get; set; }
    public Guid HomeTeamId { get; set; }
    public Guid AwayTeamId { get; set; }
    public DateOnly MatchDate { get; set; }
    public TimeOnly? KickOff { get; set; }
    public string Status { get; set; } = "scheduled"; // scheduled, completed, postponed, cancelled

    // Results
    public int? HomeGoals { get; set; }
    public int? AwayGoals { get; set; }
    public string? Result { get; set; } // H, D, A

    // Stats
    public int? HomeCorners { get; set; }
    public int? AwayCorners { get; set; }
    public int? HomeSot { get; set; }
    public int? AwaySot { get; set; }

    // xG Data
    public decimal? HomeXg { get; set; }
    public decimal? AwayXg { get; set; }
    public string? XgSource { get; set; } // understat, statsbomb, manual

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Season Season { get; set; } = null!;
    public Team HomeTeam { get; set; } = null!;
    public Team AwayTeam { get; set; } = null!;

    public ICollection<Odds> Odds { get; set; } = new List<Odds>();
    public ICollection<ModelPrediction> ModelPredictions { get; set; } = new List<ModelPrediction>();
    public ICollection<ValueBet> ValueBets { get; set; } = new List<ValueBet>();
}
