namespace BetBuilder.MathEngine.Models;

public record MarketProbabilities(
    double HomeWin,
    double Draw,
    double AwayWin,
    double Over15,
    double Under15,
    double Over25,
    double Under25,
    double Dc1X,
    double Dc12,
    double DcX2
);

public record MatchPredictionResult(
    double PredictedHomeXg,
    double PredictedAwayXg,
    MarketProbabilities Probabilities,
    double[,] ScoreMatrix
);
