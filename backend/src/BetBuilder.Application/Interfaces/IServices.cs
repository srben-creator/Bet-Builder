using BetBuilder.Application.DTOs;

namespace BetBuilder.Application.Interfaces;

public interface ISyncNotifier
{
    Task SendProgressAsync(string message, CancellationToken ct = default);
}

public interface IValueBetsService
{
    Task<List<ValueBetDto>> GetValueBetsAsync(double minEdge = 2.0, Guid? leagueId = null, string? date = null, CancellationToken ct = default);
    Task<List<LeagueDto>> GetActiveLeaguesAsync(CancellationToken ct = default);
}

public interface ILadderService
{
    Task<LadderCurrentDto> GetCurrentChallengeAsync(CancellationToken ct = default);
    Task<List<LadderSafeLegDto>> GetSafeLegsAsync(CancellationToken ct = default);
    Task<LadderCurrentDto> WinStepAsync(decimal odds, CancellationToken ct = default);
    Task<LadderCurrentDto> LoseStepAsync(CancellationToken ct = default);
    Task<LadderCurrentDto> ResetChallengeAsync(CancellationToken ct = default);
}

public interface IFixturesService
{
    Task<List<FixtureDto>> GetUpcomingFixturesAsync(List<Guid>? leagueIds = null, CancellationToken ct = default);
}

public interface IPerformanceService
{
    Task<PerformanceDashboardDto> GetPerformanceDashboardAsync(CancellationToken ct = default);
}

public interface IBacktestService
{
    Task<BacktestReportDto> GetBacktestReportAsync(CancellationToken ct = default);
}

public interface IDataSyncService
{
    Task<SyncResultDto> SyncLiveOddsAndPredictAsync(CancellationToken ct = default);
    Task<SyncResultDto> SyncWeekendResultsAndSettleAsync(CancellationToken ct = default);
}
