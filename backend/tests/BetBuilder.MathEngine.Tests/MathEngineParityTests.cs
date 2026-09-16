using BetBuilder.MathEngine.Core;
using BetBuilder.MathEngine.Models;
using Xunit;

namespace BetBuilder.MathEngine.Tests;

public class MathEngineParityTests
{
    [Fact]
    public void BivariatePoisson_ShouldGenerateNormalizedMatrixAndValidProbabilities()
    {
        // Arrange
        double lambdaHome = 1.65;
        double lambdaAway = 1.15;
        double rho = -0.13;

        // Act
        var matrix = BivariatePoisson.GenerateScoreMatrix(lambdaHome, lambdaAway, rho);
        var probs = BivariatePoisson.ExtractMarketProbabilities(matrix);

        // Assert - Matrix sum is 1.0
        double matrixSum = 0.0;
        for (int h = 0; h < matrix.GetLength(0); h++)
        {
            for (int a = 0; a < matrix.GetLength(1); a++)
            {
                matrixSum += matrix[h, a];
            }
        }
        Assert.Equal(1.0, matrixSum, precision: 5);

        // Assert - 1X2 sum is 1.0
        double sum1X2 = probs.HomeWin + probs.Draw + probs.AwayWin;
        Assert.Equal(1.0, sum1X2, precision: 5);

        // Assert - Over/Under sums to 1.0
        Assert.Equal(1.0, probs.Over15 + probs.Under15, precision: 5);
        Assert.Equal(1.0, probs.Over25 + probs.Under25, precision: 5);

        // Assert - Double Chance consistency
        Assert.Equal(probs.HomeWin + probs.Draw, probs.Dc1X, precision: 5);
        Assert.Equal(probs.HomeWin + probs.AwayWin, probs.Dc12, precision: 5);
        Assert.Equal(probs.Draw + probs.AwayWin, probs.DcX2, precision: 5);
    }

    [Theory]
    [InlineData(0.50, 2.20, 10.0, 4.55, 2.08)]
    [InlineData(0.60, 2.00, 20.0, 10.0, 5.00)]
    [InlineData(0.30, 2.50, -25.0, -10.0, 0.0)] // Negative EV should give 0% Kelly
    public void KellyCalculator_ShouldCalculateAccurateEvAndQuarterKelly(
        double prob, double odds, double expectedEv, double expectedEdge, double expectedQuarterKelly)
    {
        // Act
        var ev = KellyCalculator.CalculateEv(prob, odds);
        var edge = KellyCalculator.CalculateEdge(prob, odds);
        var kelly = KellyCalculator.CalculateQuarterKelly(prob, odds);

        // Assert
        Assert.True(Math.Abs(expectedEv - ev) < 0.1, $"EV mismatch: expected {expectedEv}, got {ev}");
        Assert.True(Math.Abs(expectedEdge - edge) < 0.1, $"Edge mismatch: expected {expectedEdge}, got {edge}");
        Assert.True(Math.Abs(expectedQuarterKelly - kelly) < 0.1, $"Kelly mismatch: expected {expectedQuarterKelly}, got {kelly}");
    }

    [Fact]
    public void KellyCalculator_PinnacleSharpFilter_ShouldRejectSoftOddsBelowPinnacle()
    {
        // Pinnacle is 2.10, soft bookie offers 2.05 -> Rejected!
        Assert.False(KellyCalculator.IsPinnacleSharpLineBeaten(2.05, 2.10));

        // Soft bookie offers 2.15 -> Accepted!
        Assert.True(KellyCalculator.IsPinnacleSharpLineBeaten(2.15, 2.10));

        // Pinnacle is null or not available -> Accepted!
        Assert.True(KellyCalculator.IsPinnacleSharpLineBeaten(2.15, null));
    }

    [Fact]
    public void DixonColesEngine_ShouldFitAndMaintainAverageAttackConstraint()
    {
        // Arrange - Synthetic historical matches
        var matches = new List<MatchTrainingRecord>
        {
            new("Arsenal", "Chelsea", 1.8, 0.9, 10, 2, 1),
            new("Chelsea", "Liverpool", 1.2, 1.5, 15, 1, 2),
            new("Liverpool", "Arsenal", 2.1, 1.4, 20, 2, 2),
            new("Arsenal", "Man City", 1.4, 1.6, 25, 1, 2),
            new("Man City", "Chelsea", 2.5, 0.8, 30, 3, 0),
            new("Liverpool", "Man City", 1.9, 1.8, 35, 1, 1),
        };

        var engine = new DixonColesEngine();

        // Act
        engine.Fit(matches);
        var paramsResult = engine.GetParameters();

        // Assert
        Assert.NotNull(paramsResult);
        Assert.Equal(4, paramsResult.AttackStrengths.Count);

        // Mean of attack strengths must be exactly 1.0
        var meanAttack = paramsResult.AttackStrengths.Values.Average();
        Assert.Equal(1.0, meanAttack, precision: 4);

        // Predict a match
        var pred = engine.PredictMatch("Arsenal", "Chelsea");
        Assert.True(pred.PredictedHomeXg > 0);
        Assert.True(pred.PredictedAwayXg > 0);
        Assert.True(pred.Probabilities.HomeWin > 0);
        Assert.True(pred.Probabilities.Draw > 0);
        Assert.True(pred.Probabilities.AwayWin > 0);
    }
}
