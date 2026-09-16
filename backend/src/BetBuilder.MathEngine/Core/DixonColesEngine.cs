using BetBuilder.MathEngine.Models;

namespace BetBuilder.MathEngine.Core;

public class DixonColesEngine
{
    private readonly double _decayRate;
    private readonly double _rho;
    private readonly List<string> _teams = new();
    private readonly Dictionary<string, int> _teamToIdx = new(StringComparer.OrdinalIgnoreCase);

    private double[]? _attackStrengths;
    private double[]? _defenseStrengths;
    private double _homeAdvantage = 1.1;

    public DixonColesEngine(double decayRate = 0.0065, double rho = -0.13)
    {
        _decayRate = decayRate;
        _rho = rho;
    }

    public TeamStrengthParameters? GetParameters()
    {
        if (_attackStrengths == null || _defenseStrengths == null) return null;

        var attacks = new Dictionary<string, double>();
        var defenses = new Dictionary<string, double>();

        for (int i = 0; i < _teams.Count; i++)
        {
            attacks[_teams[i]] = _attackStrengths[i];
            defenses[_teams[i]] = _defenseStrengths[i];
        }

        return new TeamStrengthParameters(attacks, defenses, _homeAdvantage, _rho);
    }

    public void Fit(IReadOnlyList<MatchTrainingRecord> matches)
    {
        if (matches.Count == 0)
            throw new ArgumentException("Cannot fit model with empty matches dataset.", nameof(matches));

        // 1. Map Teams
        _teams.Clear();
        _teamToIdx.Clear();

        var teamSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in matches)
        {
            teamSet.Add(m.HomeTeam);
            teamSet.Add(m.AwayTeam);
        }

        _teams.AddRange(teamSet.OrderBy(t => t));
        for (int i = 0; i < _teams.Count; i++)
        {
            _teamToIdx[_teams[i]] = i;
        }

        int n = _teams.Count;

        // 2. Adjust xG using team finishing variance ratio (goals / xG) clamped to [0.7, 1.3]
        var teamGoals = new Dictionary<string, double>();
        var teamXg = new Dictionary<string, double>();

        foreach (var t in _teams)
        {
            teamGoals[t] = 0.0;
            teamXg[t] = 0.0;
        }

        foreach (var m in matches)
        {
            if (m.HomeGoals.HasValue)
            {
                teamGoals[m.HomeTeam] += m.HomeGoals.Value;
                teamXg[m.HomeTeam] += m.HomeXg;
            }
            if (m.AwayGoals.HasValue)
            {
                teamGoals[m.AwayTeam] += m.AwayGoals.Value;
                teamXg[m.AwayTeam] += m.AwayXg;
            }
        }

        var teamRatios = new Dictionary<string, double>();
        foreach (var t in _teams)
        {
            var xg = teamXg[t];
            if (xg > 0)
            {
                var ratio = teamGoals[t] / xg;
                teamRatios[t] = Math.Clamp(ratio, 0.7, 1.3);
            }
            else
            {
                teamRatios[t] = 1.0;
            }
        }

        // 3. Prepare match vectors
        int mCount = matches.Count;
        var homeIdxs = new int[mCount];
        var awayIdxs = new int[mCount];
        var homeXgs = new double[mCount];
        var awayXgs = new double[mCount];
        var weights = new double[mCount];

        for (int i = 0; i < mCount; i++)
        {
            var m = matches[i];
            homeIdxs[i] = _teamToIdx[m.HomeTeam];
            awayIdxs[i] = _teamToIdx[m.AwayTeam];
            homeXgs[i] = m.HomeXg * teamRatios[m.HomeTeam];
            awayXgs[i] = m.AwayXg * teamRatios[m.AwayTeam];
            weights[i] = Math.Exp(-_decayRate * m.DaysAgo);
        }

        // 4. Initialize parameters
        var attack = new double[n];
        var defense = new double[n];
        Array.Fill(attack, 1.0);
        Array.Fill(defense, 1.0);
        var homeAdv = 1.1;

        // Adam optimizer states
        var mAtt = new double[n];
        var vAtt = new double[n];
        var mDef = new double[n];
        var vDef = new double[n];
        double mAdv = 0.0, vAdv = 0.0;

        const double lr = 0.02;
        const double beta1 = 0.9;
        const double beta2 = 0.999;
        const double eps = 1e-8;
        const int maxEpochs = 300;

        for (int epoch = 1; epoch <= maxEpochs; epoch++)
        {
            var gradAtt = new double[n];
            var gradDef = new double[n];
            double gradAdv = 0.0;

            // Compute analytical gradients of weighted least squares loss
            for (int k = 0; k < mCount; k++)
            {
                int h = homeIdxs[k];
                int a = awayIdxs[k];
                double w = weights[k];

                double predHome = attack[h] * defense[a] * homeAdv;
                double predAway = attack[a] * defense[h];

                double errHome = predHome - homeXgs[k];
                double errAway = predAway - awayXgs[k];

                // Home match error contribution
                gradAtt[h] += 2.0 * w * errHome * (defense[a] * homeAdv);
                gradDef[a] += 2.0 * w * errHome * (attack[h] * homeAdv);
                gradAdv    += 2.0 * w * errHome * (attack[h] * defense[a]);

                // Away match error contribution
                gradAtt[a] += 2.0 * w * errAway * defense[h];
                gradDef[h] += 2.0 * w * errAway * attack[a];
            }

            // Constraint penalty: mean(attack) == 1.0
            double meanAtt = attack.Average();
            double penGrad = 200.0 * (meanAtt - 1.0) / n;
            for (int i = 0; i < n; i++)
            {
                gradAtt[i] += penGrad;
            }

            // Adam update for attack
            for (int i = 0; i < n; i++)
            {
                mAtt[i] = beta1 * mAtt[i] + (1.0 - beta1) * gradAtt[i];
                vAtt[i] = beta2 * vAtt[i] + (1.0 - beta2) * (gradAtt[i] * gradAtt[i]);
                double mHat = mAtt[i] / (1.0 - Math.Pow(beta1, epoch));
                double vHat = vAtt[i] / (1.0 - Math.Pow(beta2, epoch));
                attack[i] -= lr * mHat / (Math.Sqrt(vHat) + eps);
                attack[i] = Math.Clamp(attack[i], 0.1, 5.0);
            }

            // Adam update for defense
            for (int i = 0; i < n; i++)
            {
                mDef[i] = beta1 * mDef[i] + (1.0 - beta1) * gradDef[i];
                vDef[i] = beta2 * vDef[i] + (1.0 - beta2) * (gradDef[i] * gradDef[i]);
                double mHat = mDef[i] / (1.0 - Math.Pow(beta1, epoch));
                double vHat = vDef[i] / (1.0 - Math.Pow(beta2, epoch));
                defense[i] -= lr * mHat / (Math.Sqrt(vHat) + eps);
                defense[i] = Math.Clamp(defense[i], 0.1, 5.0);
            }

            // Adam update for home advantage
            mAdv = beta1 * mAdv + (1.0 - beta1) * gradAdv;
            vAdv = beta2 * vAdv + (1.0 - beta2) * (gradAdv * gradAdv);
            double mAdvHat = mAdv / (1.0 - Math.Pow(beta1, epoch));
            double vAdvHat = vAdv / (1.0 - Math.Pow(beta2, epoch));
            homeAdv -= lr * mAdvHat / (Math.Sqrt(vAdvHat) + eps);
            homeAdv = Math.Clamp(homeAdv, 0.5, 2.0);
        }

        // 5. Final exact normalization: mean(attack) = 1.0
        double finalMeanAtt = attack.Average();
        if (finalMeanAtt > 0)
        {
            for (int i = 0; i < n; i++)
            {
                attack[i] /= finalMeanAtt;
                defense[i] *= finalMeanAtt;
            }
        }

        _attackStrengths = attack;
        _defenseStrengths = defense;
        _homeAdvantage = homeAdv;
    }

    public MatchPredictionResult PredictMatch(string homeTeam, string awayTeam)
    {
        if (_attackStrengths == null || _defenseStrengths == null)
            throw new InvalidOperationException("Model has not been trained yet. Call Fit() first.");

        if (!_teamToIdx.TryGetValue(homeTeam, out int hIdx) || !_teamToIdx.TryGetValue(awayTeam, out int aIdx))
            throw new KeyNotFoundException($"One or both teams ('{homeTeam}', '{awayTeam}') not found in training data.");

        double predHomeXg = _attackStrengths[hIdx] * _defenseStrengths[aIdx] * _homeAdvantage;
        double predAwayXg = _attackStrengths[aIdx] * _defenseStrengths[hIdx];

        var scoreMatrix = BivariatePoisson.GenerateScoreMatrix(predHomeXg, predAwayXg, _rho);
        var probs = BivariatePoisson.ExtractMarketProbabilities(scoreMatrix);

        return new MatchPredictionResult(predHomeXg, predAwayXg, probs, scoreMatrix);
    }
}
