using BetBuilder.Application.DTOs;
using BetBuilder.Application.Interfaces;
using BetBuilder.Domain.Entities;
using BetBuilder.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BetBuilder.Application.Services;

public class LadderService : ILadderService
{
    private readonly BetBuilderDbContext _db;

    public LadderService(BetBuilderDbContext db)
    {
        _db = db;
    }

    public async Task<LadderCurrentDto> GetCurrentChallengeAsync(CancellationToken ct = default)
    {
        var active = await _db.LadderChallenges
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync(c => c.Status == "active", ct);

        if (active == null)
        {
            active = new LadderChallenge
            {
                ChallengeNumber = 1,
                InitialStake = 5.00m,
                TargetAmount = 50.00m,
                CurrentBankroll = 5.00m,
                CurrentStep = 1,
                Status = "active",
                CreatedAt = DateTime.UtcNow
            };
            _db.LadderChallenges.Add(active);
            await _db.SaveChangesAsync(ct);
        }

        return new LadderCurrentDto(
            active.Id,
            active.ChallengeNumber,
            active.CurrentBankroll,
            active.TargetAmount,
            active.CurrentStep,
            active.Status
        );
    }

    public async Task<List<LadderSafeLegDto>> GetSafeLegsAsync(CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var predictions = await _db.ModelPredictions
            .AsNoTracking()
            .Include(p => p.Fixture)
                .ThenInclude(f => f.HomeTeam)
            .Include(p => p.Fixture)
                .ThenInclude(f => f.AwayTeam)
            .Where(p => p.Fixture.Status == "scheduled" && p.Fixture.MatchDate >= today)
            .ToListAsync(ct);

        var safeLegs = new List<LadderSafeLegDto>();

        foreach (var p in predictions)
        {
            var fix = p.Fixture;
            var home = fix.HomeTeam?.Name ?? "Home";
            var away = fix.AwayTeam?.Name ?? "Away";
            var matchTime = fix.KickOff.HasValue ? $" ({fix.KickOff.Value:HH:mm})" : "";
            var match = $"{home} vs {away}{matchTime}";
            var dateStr = fix.MatchDate.ToString("yyyy-MM-dd");

            if (p.Over15Prob.HasValue && (double)p.Over15Prob.Value > 0.75)
            {
                safeLegs.Add(new LadderSafeLegDto(
                    Date: dateStr,
                    FixtureId: fix.Id,
                    Match: match,
                    Market: "Over 1.5 Goals",
                    Selection: "Over",
                    Prob: (double)p.Over15Prob.Value
                ));
            }

            if (p.Dc1XProb.HasValue && (double)p.Dc1XProb.Value > 0.80)
            {
                safeLegs.Add(new LadderSafeLegDto(
                    Date: dateStr,
                    FixtureId: fix.Id,
                    Match: match,
                    Market: "1X (Home or Draw)",
                    Selection: "1X",
                    Prob: (double)p.Dc1XProb.Value
                ));
            }

            if (p.DcX2Prob.HasValue && (double)p.DcX2Prob.Value > 0.80)
            {
                safeLegs.Add(new LadderSafeLegDto(
                    Date: dateStr,
                    FixtureId: fix.Id,
                    Match: match,
                    Market: "X2 (Away or Draw)",
                    Selection: "X2",
                    Prob: (double)p.DcX2Prob.Value
                ));
            }
        }

        return safeLegs.OrderByDescending(l => l.Prob).ToList();
    }

    public async Task<LadderCurrentDto> WinStepAsync(decimal odds, CancellationToken ct = default)
    {
        var active = await _db.LadderChallenges
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync(c => c.Status == "active", ct);

        if (active == null)
        {
            return await GetCurrentChallengeAsync(ct);
        }

        if (odds > 1.0m)
        {
            active.CurrentBankroll = Math.Round(active.CurrentBankroll * odds, 2);
            active.CurrentStep += 1;

            if (active.CurrentBankroll >= active.TargetAmount)
            {
                active.Status = "completed";
                active.CompletedAt = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync(ct);
        }

        return new LadderCurrentDto(
            active.Id,
            active.ChallengeNumber,
            active.CurrentBankroll,
            active.TargetAmount,
            active.CurrentStep,
            active.Status
        );
    }

    public async Task<LadderCurrentDto> LoseStepAsync(CancellationToken ct = default)
    {
        var active = await _db.LadderChallenges
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync(c => c.Status == "active", ct);

        if (active != null)
        {
            active.Status = "busted";
            active.CompletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }

        return await ResetChallengeAsync(ct);
    }

    public async Task<LadderCurrentDto> ResetChallengeAsync(CancellationToken ct = default)
    {
        var active = await _db.LadderChallenges
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync(c => c.Status == "active", ct);

        if (active != null)
        {
            active.CurrentBankroll = active.InitialStake;
            active.CurrentStep = 1;
            active.Status = "active";
            active.CompletedAt = null;
            await _db.SaveChangesAsync(ct);
        }
        else
        {
            active = new LadderChallenge
            {
                ChallengeNumber = 1,
                InitialStake = 5.00m,
                TargetAmount = 50.00m,
                CurrentBankroll = 5.00m,
                CurrentStep = 1,
                Status = "active",
                CreatedAt = DateTime.UtcNow
            };
            _db.LadderChallenges.Add(active);
            await _db.SaveChangesAsync(ct);
        }

        return new LadderCurrentDto(
            active.Id,
            active.ChallengeNumber,
            active.CurrentBankroll,
            active.TargetAmount,
            active.CurrentStep,
            active.Status
        );
    }
}
