using BetBuilder.MathEngine.Models;
using MathNet.Numerics.Distributions;

namespace BetBuilder.MathEngine.Core;

public static class BivariatePoisson
{
    public static double RhoCorrection(int x, int y, double lambdaX, double lambdaY, double rho)
    {
        if (x == 0 && y == 0)
            return 1.0 - (lambdaX * lambdaY * rho);
        if (x == 0 && y == 1)
            return 1.0 + (lambdaX * rho);
        if (x == 1 && y == 0)
            return 1.0 + (lambdaY * rho);
        if (x == 1 && y == 1)
            return 1.0 - rho;
        return 1.0;
    }

    public static double[,] GenerateScoreMatrix(double lambdaHome, double lambdaAway, double rho = -0.13, int maxGoals = 9)
    {
        var size = maxGoals + 1;
        var matrix = new double[size, size];
        var totalSum = 0.0;

        for (int h = 0; h < size; h++)
        {
            var probHome = Poisson.PMF(lambdaHome, h);
            for (int a = 0; a < size; a++)
            {
                var probAway = Poisson.PMF(lambdaAway, a);
                var correction = RhoCorrection(h, a, lambdaHome, lambdaAway, rho);
                var jointProb = Math.Max(0.0, probHome * probAway * correction);
                matrix[h, a] = jointProb;
                totalSum += jointProb;
            }
        }

        // Normalize matrix sum to 1.0
        if (totalSum > 0)
        {
            for (int h = 0; h < size; h++)
            {
                for (int a = 0; a < size; a++)
                {
                    matrix[h, a] /= totalSum;
                }
            }
        }

        return matrix;
    }

    public static MarketProbabilities ExtractMarketProbabilities(double[,] scoreMatrix)
    {
        var rows = scoreMatrix.GetLength(0);
        var cols = scoreMatrix.GetLength(1);

        double homeWin = 0.0;
        double draw = 0.0;
        double awayWin = 0.0;
        double under15 = 0.0;
        double under25 = 0.0;

        for (int h = 0; h < rows; h++)
        {
            for (int a = 0; a < cols; a++)
            {
                var p = scoreMatrix[h, a];
                if (h > a) homeWin += p;
                else if (h == a) draw += p;
                else awayWin += p;

                if (h + a < 1.5) under15 += p;
                if (h + a < 2.5) under25 += p;
            }
        }

        var over15 = Math.Max(0.0, 1.0 - under15);
        var over25 = Math.Max(0.0, 1.0 - under25);
        var dc1X = homeWin + draw;
        var dc12 = homeWin + awayWin;
        var dcX2 = draw + awayWin;

        return new MarketProbabilities(
            HomeWin: homeWin,
            Draw: draw,
            AwayWin: awayWin,
            Over15: over15,
            Under15: under15,
            Over25: over25,
            Under25: under25,
            Dc1X: dc1X,
            Dc12: dc12,
            DcX2: dcX2
        );
    }
}
