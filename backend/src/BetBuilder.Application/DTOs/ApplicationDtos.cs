namespace BetBuilder.Application.DTOs;

public record LeagueDto(Guid Id, string Name, string Country, string Code, string? FdCsvCode, string? UnderstatName, bool IsActive);
public record UpdateLeagueDto(bool IsActive, string? UnderstatName);

public record ValueBetDto(
    Guid Id,
    Guid FixtureId,
    string Date,
    string League,
    string Match,
    string Market,
    string Selection,
    double ModelProb,
    string ProbPercent,
    string TrueOdds,
    string Pinnacle,
    string Bookmaker,
    double Odds,
    double EdgeEv,
    string QKellyStake
);

public record LadderCurrentDto(
    Guid Id,
    int ChallengeNumber,
    decimal CurrentBankroll,
    decimal TargetAmount,
    int CurrentStep,
    string Status
);

public record LadderSafeLegDto(
    string Date,
    Guid FixtureId,
    string Match,
    string Market,
    string Selection,
    double Prob
);

public record FixtureDto(
    Guid Id,
    string League,
    string Date,
    string Time,
    string HomeTeam,
    string AwayTeam,
    string Status,
    int? HomeGoals,
    int? AwayGoals
);

public record PnlPointDto(
    string Date,
    decimal CumulativePnl,
    string Market,
    decimal Odds,
    decimal Pnl
);

public record SettledBetDto(
    Guid Id,
    string Date,
    string Market,
    string Selection,
    string Result,
    decimal Odds,
    decimal Stake,
    decimal Pnl
);

public record PerformanceDashboardDto(
    int TotalSettledBets,
    double WinRate,
    decimal TotalProfit,
    double Roi,
    List<PnlPointDto> BankrollEvolution,
    List<SettledBetDto> History
);

public record MarketStatDto(
    string Market,
    int Bets,
    double WinRate,
    decimal TotalStaked,
    decimal TotalProfit,
    double Roi
);

public record BacktestReportDto(
    int TotalSimulatedBets,
    double WinRate,
    double OverallRoi,
    double BeatClosingRate,
    double AvgClvEdge,
    List<PnlPointDto> CumulativePnlEvolution,
    List<MarketStatDto> MarketStats
);

public record SyncResultDto(
    bool Success,
    string Message,
    int FixturesUpdated = 0,
    int OddsUpdated = 0,
    int ValueBetsGenerated = 0,
    int BetsSettled = 0
);
