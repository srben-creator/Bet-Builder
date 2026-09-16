namespace BetBuilder.Domain.Entities;

public class ModelPrediction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FixtureId { get; set; }
    public string ModelVersion { get; set; } = "v1";

    public decimal HomeWinProb { get; set; }
    public decimal DrawProb { get; set; }
    public decimal AwayWinProb { get; set; }

    public decimal? Dc1XProb { get; set; }
    public decimal? Dc12Prob { get; set; }
    public decimal? DcX2Prob { get; set; }

    public decimal? Over15Prob { get; set; }
    public decimal? Over25Prob { get; set; }

    public decimal? PredictedHomeGoals { get; set; }
    public decimal? PredictedAwayGoals { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Fixture Fixture { get; set; } = null!;
}
