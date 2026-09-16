namespace BetBuilder.MathEngine.Models;

public record MatchTrainingRecord(
    string HomeTeam,
    string AwayTeam,
    double HomeXg,
    double AwayXg,
    double DaysAgo,
    int? HomeGoals = null,
    int? AwayGoals = null
);
