using BetBuilder.Application.DTOs;
using BetBuilder.Application.Interfaces;
using BetBuilder.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BetBuilder.Application.Services;

public class PerformanceService : IPerformanceService
{
    private readonly BetBuilderDbContext _db;

    public PerformanceService(BetBuilderDbContext db)
    {
        _db = db;
    }

    public async Task<PerformanceDashboardDto> GetPerformanceDashboardAsync(CancellationToken ct = default)
    {
        var bets = await _db.ValueBets
            .AsNoTracking()
            .Where(b => b.Status == "won" || b.Status == "lost")
            .OrderBy(b => b.CreatedAt)
            .ToListAsync(ct);

        if (bets.Count == 0)
        {
            return new PerformanceDashboardDto(
                TotalSettledBets: 0,
                WinRate: 0.0,
                TotalProfit: 0m,
                Roi: 0.0,
                BankrollEvolution: new List<PnlPointDto>(),
                History: new List<SettledBetDto>()
            );
        }

        int totalBets = bets.Count;
        int wonBets = bets.Count(b => b.Status == "won");
        double winRate = (double)wonBets / totalBets * 100.0;

        decimal totalStaked = bets.Sum(b => b.RecommendedStake ?? 1.0m);
        decimal totalProfit = bets.Sum(b => b.Pnl ?? 0m);
        double roi = totalStaked > 0 ? (double)(totalProfit / totalStaked) * 100.0 : 0.0;

        decimal cumulative = 0m;
        var evolution = new List<PnlPointDto>();
        var history = new List<SettledBetDto>();

        foreach (var bet in bets)
        {
            var pnl = bet.Pnl ?? 0m;
            cumulative += pnl;

            evolution.Add(new PnlPointDto(
                Date: bet.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
                CumulativePnl: Math.Round(cumulative, 2),
                Market: bet.Market,
                Odds: bet.OddsPrice,
                Pnl: Math.Round(pnl, 2)
            ));

            history.Add(new SettledBetDto(
                Id: bet.Id,
                Date: bet.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
                Market: bet.Market,
                Selection: bet.Selection,
                Result: bet.Status,
                Odds: bet.OddsPrice,
                Stake: bet.RecommendedStake ?? 1.0m,
                Pnl: Math.Round(pnl, 2)
            ));
        }

        history.Reverse(); // Newest first

        return new PerformanceDashboardDto(
            TotalSettledBets: totalBets,
            WinRate: Math.Round(winRate, 1),
            TotalProfit: Math.Round(totalProfit, 2),
            Roi: Math.Round(roi, 1),
            BankrollEvolution: evolution,
            History: history
        );
    }
}
