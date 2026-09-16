using BetBuilder.Application.DTOs;
using BetBuilder.Application.Interfaces;
using BetBuilder.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BetBuilder.Application.Services;

public class BacktestService : IBacktestService
{
    private readonly BetBuilderDbContext _db;

    public BacktestService(BetBuilderDbContext db)
    {
        _db = db;
    }

    public async Task<BacktestReportDto> GetBacktestReportAsync(CancellationToken ct = default)
    {
        var results = await _db.BacktestResults
            .AsNoTracking()
            .Include(b => b.Fixture)
            .OrderBy(b => b.Fixture.MatchDate)
            .ToListAsync(ct);

        if (results.Count == 0)
        {
            return new BacktestReportDto(
                TotalSimulatedBets: 0,
                WinRate: 0.0,
                OverallRoi: 0.0,
                BeatClosingRate: 0.0,
                AvgClvEdge: 0.0,
                CumulativePnlEvolution: new List<PnlPointDto>(),
                MarketStats: new List<MarketStatDto>()
            );
        }

        int totalBets = results.Count;
        int wonBets = results.Count(r => r.ResultWon);
        double winRate = (double)wonBets / totalBets * 100.0;

        decimal totalStaked = results.Sum(r => r.KellyFraction);
        decimal totalProfit = results.Sum(r => r.Pnl);
        double overallRoi = totalStaked > 0 ? (double)(totalProfit / totalStaked) * 100.0 : 0.0;

        // CLV Metrics
        var clvBets = results.Where(r => r.ClvPct.HasValue).ToList();
        int beatClosingCount = clvBets.Count(r => r.ClvPct!.Value > 0);
        double beatClosingRate = clvBets.Count > 0 ? (double)beatClosingCount / clvBets.Count * 100.0 : 0.0;
        double avgClv = clvBets.Count > 0 ? (double)clvBets.Average(r => r.ClvPct!.Value) : 0.0;

        // Cumulative PnL
        decimal cumulative = 0m;
        var evolution = new List<PnlPointDto>();
        foreach (var r in results)
        {
            cumulative += r.Pnl;
            evolution.Add(new PnlPointDto(
                Date: r.Fixture?.MatchDate.ToString("yyyy-MM-dd") ?? r.CreatedAt.ToString("yyyy-MM-dd"),
                CumulativePnl: Math.Round(cumulative, 2),
                Market: r.Market,
                Odds: r.OddsPrice,
                Pnl: Math.Round(r.Pnl, 2)
            ));
        }

        // Market Stats
        var marketGroups = results.GroupBy(r => r.Market);
        var marketStats = new List<MarketStatDto>();

        foreach (var grp in marketGroups)
        {
            int mCount = grp.Count();
            int mWon = grp.Count(x => x.ResultWon);
            decimal mStaked = grp.Sum(x => x.KellyFraction);
            decimal mProfit = grp.Sum(x => x.Pnl);
            double mWinRate = mCount > 0 ? (double)mWon / mCount * 100.0 : 0.0;
            double mRoi = mStaked > 0 ? (double)(mProfit / mStaked) * 100.0 : 0.0;

            marketStats.Add(new MarketStatDto(
                Market: grp.Key,
                Bets: mCount,
                WinRate: Math.Round(mWinRate, 1),
                TotalStaked: Math.Round(mStaked, 2),
                TotalProfit: Math.Round(mProfit, 2),
                Roi: Math.Round(mRoi, 1)
            ));
        }

        return new BacktestReportDto(
            TotalSimulatedBets: totalBets,
            WinRate: Math.Round(winRate, 1),
            OverallRoi: Math.Round(overallRoi, 2),
            BeatClosingRate: Math.Round(beatClosingRate, 1),
            AvgClvEdge: Math.Round(avgClv, 2),
            CumulativePnlEvolution: evolution,
            MarketStats: marketStats
        );
    }
}
