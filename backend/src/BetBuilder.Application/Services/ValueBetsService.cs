using BetBuilder.Application.DTOs;
using BetBuilder.Application.Interfaces;
using BetBuilder.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BetBuilder.Application.Services;

public class ValueBetsService : IValueBetsService
{
    private readonly BetBuilderDbContext _db;

    public ValueBetsService(BetBuilderDbContext db)
    {
        _db = db;
    }

    public async Task<List<ValueBetDto>> GetValueBetsAsync(double minEdge = 2.0, Guid? leagueId = null, string? date = null, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var minEdgeDecimal = (decimal)minEdge;

        var query = _db.ValueBets
            .AsNoTracking()
            .Where(vb => vb.Fixture.Status == "scheduled" && vb.Fixture.MatchDate >= today && vb.EvPct >= minEdgeDecimal);

        if (leagueId.HasValue && leagueId.Value != Guid.Empty)
        {
            query = query.Where(vb => vb.Fixture.Season.LeagueId == leagueId.Value);
        }

        if (!string.IsNullOrWhiteSpace(date) && DateOnly.TryParse(date, out var filterDate))
        {
            query = query.Where(vb => vb.Fixture.MatchDate == filterDate);
        }

        var list = await query
            .OrderByDescending(vb => vb.EvPct)
            .Select(vb => new
            {
                vb.Id,
                vb.FixtureId,
                vb.Market,
                vb.Selection,
                vb.ModelProb,
                vb.OddsPrice,
                vb.EvPct,
                vb.KellyFraction,
                HomeTeamName = vb.Fixture.HomeTeam != null ? vb.Fixture.HomeTeam.Name : "Home",
                AwayTeamName = vb.Fixture.AwayTeam != null ? vb.Fixture.AwayTeam.Name : "Away",
                LeagueName = vb.Fixture.Season != null && vb.Fixture.Season.League != null ? vb.Fixture.Season.League.Name : "Unknown",
                MatchDate = vb.Fixture.MatchDate,
                KickOff = vb.Fixture.KickOff,
                BookmakerName = vb.Bookmaker != null ? vb.Bookmaker.Name : "Soft Bookie"
            })
            .ToListAsync(ct);

        if (list.Count == 0)
        {
            return new List<ValueBetDto>();
        }

        // Find Pinnacle sharp odds for these fixtures
        var fixtureIds = list.Select(vb => vb.FixtureId).Distinct().ToList();
        var pinnacleOdds = await _db.Odds
            .AsNoTracking()
            .Where(o => fixtureIds.Contains(o.FixtureId) && o.Bookmaker.Code.ToLower() == "pinnacle")
            .OrderBy(o => o.CapturedAt)
            .Select(o => new { o.FixtureId, o.Market, o.Selection, o.Price })
            .ToListAsync(ct);

        var pinnacleMap = pinnacleOdds
            .GroupBy(o => (o.FixtureId, o.Market, o.Selection))
            .ToDictionary(
                g => g.Key,
                g => g.Last().Price
            );

        var result = new List<ValueBetDto>();
        foreach (var item in list)
        {
            var home = item.HomeTeamName;
            var away = item.AwayTeamName;
            var league = item.LeagueName;
            var matchTime = item.KickOff.HasValue ? $" ({item.KickOff.Value:HH:mm})" : "";
            var match = $"{home} vs {away}{matchTime}";

            pinnacleMap.TryGetValue((item.FixtureId, item.Market, item.Selection), out var pinPrice);
            var pinStr = pinPrice > 0 ? pinPrice.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) : "N/A";

            var modelProb = (double)item.ModelProb;
            var trueOdds = modelProb > 0 ? (1.0 / modelProb).ToString("F2", System.Globalization.CultureInfo.InvariantCulture) : "0.00";

            result.Add(new ValueBetDto(
                Id: item.Id,
                FixtureId: item.FixtureId,
                Date: item.MatchDate.ToString("yyyy-MM-dd"),
                League: league,
                Match: match,
                Market: item.Market,
                Selection: item.Selection,
                ModelProb: modelProb,
                ProbPercent: $"{(modelProb * 100).ToString("F1", System.Globalization.CultureInfo.InvariantCulture)}%",
                TrueOdds: trueOdds,
                Pinnacle: pinStr,
                Bookmaker: item.BookmakerName,
                Odds: (double)item.OddsPrice,
                EdgeEv: (double)item.EvPct,
                QKellyStake: $"{(item.KellyFraction ?? 0).ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}%"
            ));
        }

        return result;
    }
}
