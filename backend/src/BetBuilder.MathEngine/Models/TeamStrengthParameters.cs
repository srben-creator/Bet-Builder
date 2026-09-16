namespace BetBuilder.MathEngine.Models;

public record TeamStrengthParameters(
    Dictionary<string, double> AttackStrengths,
    Dictionary<string, double> DefenseStrengths,
    double HomeAdvantage,
    double Rho
);
