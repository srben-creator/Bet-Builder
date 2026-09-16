using BetBuilder.Application.DTOs;
using BetBuilder.Application.Interfaces;
using BetBuilder.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BetBuilder.Application.Services;

public class FixturesService : IFixturesService
{
    private readonly BetBuilderDbContext _db;

    public FixturesService(BetBuilderDbContext db)
    {
        _db = db;
    }

    public async Task<List<FixtureDto>> GetUpcomingFixturesAsync(List<Guid>? leagueIds = null, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var query = _db.Fixtures
            .AsNoTracking()
            .Include(f => f.HomeTeam)
            .Include(f => f.AwayTeam)
            .Include(f => f.Season)
                .ThenInclude(s => s.League)
            .Where(f => f.Status == "scheduled" && f.MatchDate >= today);

        if (leagueIds != null && leagueIds.Count > 0)
        {
            query = query.Where(f => leagueIds.Contains(f.Season.LeagueId));
        }

        var fixtures = await query
            .OrderBy(f => f.MatchDate)
            .ThenBy(f => f.KickOff)
            .ToListAsync(ct);

        return fixtures.Select(f => new FixtureDto(
            Id: f.Id,
            League: f.Season?.League != null ? $"{f.Season.League.Name} ({f.Season.League.Country})" : "Unknown",
            Date: f.MatchDate.ToString("yyyy-MM-dd"),
            Time: f.KickOff.HasValue ? f.KickOff.Value.ToString("HH:mm") : "TBD",
            HomeTeam: f.HomeTeam?.Name ?? "Home",
            AwayTeam: f.AwayTeam?.Name ?? "Away",
            Status: f.Status,
            HomeGoals: f.HomeGoals,
            AwayGoals: f.AwayGoals
        )).ToList();
    }
}
