namespace BetBuilder.MathEngine.Core;

public static class KellyCalculator
{
    public static double CalculateEv(double modelProb, double oddsPrice)
    {
        if (oddsPrice <= 1.0 || modelProb <= 0.0) return 0.0;
        return ((modelProb * oddsPrice) - 1.0) * 100.0;
    }

    public static double CalculateEdge(double modelProb, double oddsPrice)
    {
        if (oddsPrice <= 1.0 || modelProb <= 0.0) return 0.0;
        var impliedProb = 1.0 / oddsPrice;
        return (modelProb - impliedProb) * 100.0;
    }

    public static double CalculateQuarterKelly(double modelProb, double oddsPrice, double fractionDivisor = 4.0)
    {
        if (oddsPrice <= 1.0 || modelProb <= 0.0) return 0.0;

        var b = oddsPrice - 1.0;
        var kelly = ((b * modelProb) - (1.0 - modelProb)) / b;

        if (kelly <= 0.0) return 0.0;

        return Math.Round((kelly / fractionDivisor) * 100.0, 2);
    }

    public static bool IsPinnacleSharpLineBeaten(double softBookiePrice, double? pinnaclePrice)
    {
        // PINNACLE FILTER RULE:
        // If Pinnacle odds exist, the soft bookie MUST offer a strictly higher price than Pinnacle.
        if (pinnaclePrice.HasValue && pinnaclePrice.Value > 1.0)
        {
            return softBookiePrice > pinnaclePrice.Value;
        }

        return true;
    }
}
