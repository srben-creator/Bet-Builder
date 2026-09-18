using BetBuilder.Application.DTOs;
using BetBuilder.Application.Interfaces;
using BetBuilder.Domain.Entities;
using BetBuilder.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BetBuilder.Application.Services;

public class LeagueService : ILeagueService
{
    private readonly BetBuilderDbContext _db;

    public LeagueService(BetBuilderDbContext db)
    {
        _db = db;
    }

    public async Task<List<LeagueDto>> GetAllLeaguesAsync(CancellationToken ct = default)
    {
        return await _db.Leagues
            .AsNoTracking()
            .OrderBy(l => l.Country)
            .ThenBy(l => l.Name)
            .Select(l => new LeagueDto(l.Id, l.Name, l.Country, l.Code, l.FdCsvCode, l.UnderstatName, l.IsActive))
            .ToListAsync(ct);
    }

    public async Task<LeagueDto> UpdateLeagueAsync(Guid id, UpdateLeagueDto dto, CancellationToken ct = default)
    {
        var league = await _db.Leagues.FirstOrDefaultAsync(l => l.Id == id, ct);
        if (league == null)
        {
            throw new Exception("League not found");
        }

        league.IsActive = dto.IsActive;
        league.UnderstatName = dto.UnderstatName;
        league.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return new LeagueDto(league.Id, league.Name, league.Country, league.Code, league.FdCsvCode, league.UnderstatName, league.IsActive);
    }

    public async Task SeedFootballDataLeaguesAsync(CancellationToken ct = default)
    {
        // Disable the incorrect duplicate leagues that were created by the previous version of this seed method
        var oldCodes = new[] { "ENG-PL", "ENG-CH", "ESP-L1", "ESP-L2", "GER-B1", "GER-B2", "ITA-A", "ITA-B", "FRA-L1", "FRA-L2", "POR-P1", "NED-E1", "BEL-D1", "TUR-S1", "GRE-S1", "SCO-P1" };
        var duplicateLeagues = await _db.Leagues
            .Where(l => oldCodes.Contains(l.Code) && l.IsActive)
            .ToListAsync(ct);
            
        foreach (var dup in duplicateLeagues)
        {
            dup.IsActive = false;
        }

        // Map of TheOddsApi codes to their Football-Data and Understat info
        var fdLeagues = new List<League>
        {
            new() { Name = "English Premier League", Country = "England", Code = "soccer_epl", FdCsvCode = "E0", UnderstatName = "EPL", IsActive = true },
            new() { Name = "Championship", Country = "England", Code = "soccer_efl_champ", FdCsvCode = "E1", IsActive = false },
            new() { Name = "La Liga", Country = "Spain", Code = "soccer_spain_la_liga", FdCsvCode = "SP1", UnderstatName = "La_Liga", IsActive = true },
            new() { Name = "Segunda Division", Country = "Spain", Code = "soccer_spain_segunda_division", FdCsvCode = "SP2", IsActive = false },
            new() { Name = "Bundesliga", Country = "Germany", Code = "soccer_germany_bundesliga", FdCsvCode = "D1", UnderstatName = "Bundesliga", IsActive = true },
            new() { Name = "2. Bundesliga", Country = "Germany", Code = "soccer_germany_bundesliga2", FdCsvCode = "D2", IsActive = false },
            new() { Name = "Serie A", Country = "Italy", Code = "soccer_italy_serie_a", FdCsvCode = "I1", UnderstatName = "Serie_A", IsActive = true },
            new() { Name = "Serie B", Country = "Italy", Code = "soccer_italy_serie_b", FdCsvCode = "I2", IsActive = false },
            new() { Name = "Ligue 1", Country = "France", Code = "soccer_france_ligue_one", FdCsvCode = "F1", UnderstatName = "Ligue_1", IsActive = true },
            new() { Name = "Ligue 2", Country = "France", Code = "soccer_france_ligue_two", FdCsvCode = "F2", IsActive = false },
            new() { Name = "Primeira Liga", Country = "Portugal", Code = "soccer_portugal_primeira_liga", FdCsvCode = "P1", IsActive = true },
            new() { Name = "Liga Portugal 2", Country = "Portugal", Code = "soccer_portugal_segunda_liga", FdCsvCode = null, IsActive = true },
            new() { Name = "Eredivisie", Country = "Netherlands", Code = "soccer_netherlands_eredivisie", FdCsvCode = "N1", IsActive = false },
            new() { Name = "First Division A", Country = "Belgium", Code = "soccer_belgium_first_div", FdCsvCode = "B1", IsActive = false },
            new() { Name = "Super Lig", Country = "Turkey", Code = "soccer_turkey_super_league", FdCsvCode = "T1", IsActive = false },
            new() { Name = "Super League", Country = "Greece", Code = "soccer_greece_super_league", FdCsvCode = "G1", IsActive = false },
            new() { Name = "Premiership", Country = "Scotland", Code = "soccer_scotland_premiership", FdCsvCode = "SC0", IsActive = false }
        };

        foreach (var fdLeague in fdLeagues)
        {
            // Find by TheOddsApi code
            var existing = await _db.Leagues.FirstOrDefaultAsync(l => l.Code == fdLeague.Code, ct);
            if (existing == null)
            {
                _db.Leagues.Add(fdLeague);
            }
            else
            {
                // Update existing league with FD code and understat
                existing.FdCsvCode = fdLeague.FdCsvCode;
                existing.UnderstatName = fdLeague.UnderstatName;
                if (!existing.IsActive && fdLeague.IsActive) {
                    existing.IsActive = true; // Only activate if it was already active in our seed
                }
            }
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task MergeOldLeaguesAsync(CancellationToken ct = default)
    {
        var map = new Dictionary<string, string>
        {
            { "EPL", "soccer_epl" },
            { "La_Liga", "soccer_spain_la_liga" },
            { "Bundesliga", "soccer_germany_bundesliga" },
            { "Serie_A", "soccer_italy_serie_a" },
            { "Ligue_1", "soccer_france_ligue_one" },
            { "Primeira", "soccer_portugal_primeira_liga" },
            // Also merge the accidental bad leagues if they have data
            { "ENG-PL", "soccer_epl" },
            { "ENG-CH", "soccer_efl_champ" },
            { "ESP-L1", "soccer_spain_la_liga" },
            { "ESP-L2", "soccer_spain_segunda_division" },
            { "GER-B1", "soccer_germany_bundesliga" },
            { "GER-B2", "soccer_germany_bundesliga2" },
            { "ITA-A", "soccer_italy_serie_a" },
            { "ITA-B", "soccer_italy_serie_b" },
            { "FRA-L1", "soccer_france_ligue_one" },
            { "FRA-L2", "soccer_france_ligue_two" },
            { "POR-P1", "soccer_portugal_primeira_liga" },
            { "NED-E1", "soccer_netherlands_eredivisie" },
            { "BEL-D1", "soccer_belgium_first_div" },
            { "TUR-S1", "soccer_turkey_super_league" },
            { "GRE-S1", "soccer_greece_super_league" },
            { "SCO-P1", "soccer_scotland_premiership" }
        };

        var allLeagues = await _db.Leagues.Include(l => l.Seasons).ThenInclude(s => s.Fixtures).ToListAsync(ct);

        foreach (var kvp in map)
        {
            var oldLeague = allLeagues.FirstOrDefault(l => l.Code == kvp.Key);
            var newLeague = allLeagues.FirstOrDefault(l => l.Code == kvp.Value);

            if (oldLeague != null && newLeague != null && oldLeague.Id != newLeague.Id)
            {
                // Move seasons from old to new
                foreach (var oldSeason in oldLeague.Seasons.ToList())
                {
                    var newSeason = newLeague.Seasons.FirstOrDefault(s => s.StartYear == oldSeason.StartYear);
                    if (newSeason == null)
                    {
                        oldSeason.LeagueId = newLeague.Id;
                        newLeague.Seasons.Add(oldSeason);
                    }
                    else
                    {
                        // Both have a season for this year. Move fixtures to the new season.
                        foreach (var fixture in oldSeason.Fixtures.ToList())
                        {
                            // Only move if not a duplicate
                            bool exists = newSeason.Fixtures.Any(f => f.HomeTeamId == fixture.HomeTeamId && f.AwayTeamId == fixture.AwayTeamId && f.MatchDate == fixture.MatchDate);
                            if (!exists)
                            {
                                fixture.SeasonId = newSeason.Id;
                                newSeason.Fixtures.Add(fixture);
                            }
                            else
                            {
                                // A duplicate fixture exists. Delete the old one so it doesn't block deletion of oldSeason.
                                _db.Fixtures.Remove(fixture);
                            }
                        }
                        _db.Seasons.Remove(oldSeason);
                    }
                }

                _db.Leagues.Remove(oldLeague);
            }
            else if (oldLeague != null && newLeague == null)
            {
                // Just rename the old league to the new code if the new one doesn't exist
                oldLeague.Code = kvp.Value;
            }
        }

        await _db.SaveChangesAsync(ct);
    }
}
