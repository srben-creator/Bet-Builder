using BetBuilder.Application.DTOs;
using BetBuilder.Application.Interfaces;
using BetBuilder.Domain.Entities;
using BetBuilder.Infrastructure.Data;
using BetBuilder.MathEngine.Core;
using BetBuilder.MathEngine.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text.Json;

namespace BetBuilder.Application.Services;

public class DataSyncService : IDataSyncService
{
    private readonly BetBuilderDbContext _db;
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ILogger<DataSyncService> _logger;

    private static readonly Dictionary<string, string> OddsApiSportKeys = new()
    {
        { "E0", "soccer_epl" },
        { "SP1", "soccer_spain_la_liga" },
        { "D1", "soccer_germany_bundesliga" },
        { "I1", "soccer_italy_serie_a" },
        { "F1", "soccer_france_ligue_one" },
        { "P1", "soccer_portugal_primeira_liga" }
    };

    public DataSyncService(
        BetBuilderDbContext db,
        HttpClient http,
        IConfiguration config,
        ILogger<DataSyncService> logger)
    {
        _db = db;
        _http = http;
        _config = config;
        _logger = logger;
    }

    public async Task<SyncResultDto> SyncWeekendResultsAndSettleAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Starting SyncWeekendResultsAndSettleAsync...");
        int fixturesUpdated = 0;
        int betsSettled = 0;

        // 1. Fetch active leagues with fd_csv_code
        var leagues = await _db.Leagues
            .Where(l => l.IsActive && !string.IsNullOrEmpty(l.FdCsvCode))
            .ToListAsync(ct);

        var teams = await _db.Teams.ToListAsync(ct);
        var teamMap = teams.ToDictionary(t => (t.Name.ToLowerInvariant(), t.Country.ToLowerInvariant()), t => t.Id);

        // Download recent CSV for each league from football-data.co.uk
        // Current season string e.g. "2425"
        int currentYear = DateTime.UtcNow.Year;
        int seasonStart = DateTime.UtcNow.Month < 7 ? currentYear - 1 : currentYear;
        string seasonCode = $"{seasonStart % 100:D2}{(seasonStart + 1) % 100:D2}";

        foreach (var league in leagues)
        {
            var url = $"https://www.football-data.co.uk/mmz4281/{seasonCode}/{league.FdCsvCode}.csv";
            try
            {
                var response = await _http.GetAsync(url, ct);
                if (!response.IsSuccessStatusCode)
                {
                    continue;
                }

                var csvContent = await response.Content.ReadAsStringAsync(ct);
                var lines = csvContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                if (lines.Length < 2)
                {
                    continue;
                }

                var headers = lines[0].Split(',').Select(h => h.Trim()).ToArray();
                int idxDate = Array.IndexOf(headers, "Date");
                int idxHome = Array.IndexOf(headers, "HomeTeam");
                int idxAway = Array.IndexOf(headers, "AwayTeam");
                int idxFthg = Array.IndexOf(headers, "FTHG");
                int idxFtag = Array.IndexOf(headers, "FTAG");
                int idxFtr = Array.IndexOf(headers, "FTR");

                if (idxDate < 0 || idxHome < 0 || idxAway < 0 || idxFthg < 0 || idxFtag < 0)
                {
                    continue;
                }

                var season = await _db.Seasons
                    .FirstOrDefaultAsync(s => s.LeagueId == league.Id && s.IsCurrent, ct);

                if (season == null)
                {
                    continue;
                }

                for (int i = 1; i < lines.Length; i++)
                {
                    var cols = lines[i].Split(',');
                    if (cols.Length <= Math.Max(idxFthg, idxFtag))
                    {
                        continue;
                    }

                    var rawHome = cols[idxHome].Trim();
                    var rawAway = cols[idxAway].Trim();
                    var dateStr = cols[idxDate].Trim();
                    var fthgStr = cols[idxFthg].Trim();
                    var ftagStr = cols[idxFtag].Trim();
                    var ftrStr = idxFtr >= 0 && cols.Length > idxFtr ? cols[idxFtr].Trim() : null;

                    if (string.IsNullOrEmpty(rawHome) || string.IsNullOrEmpty(rawAway) || string.IsNullOrEmpty(fthgStr) || string.IsNullOrEmpty(ftagStr))
                    {
                        continue;
                    }

                    if (!int.TryParse(fthgStr, out int fthg) || !int.TryParse(ftagStr, out int ftag))
                    {
                        continue;
                    }

                    DateOnly matchDate;
                    if (!DateOnly.TryParseExact(dateStr, new[] { "dd/MM/yyyy", "dd/MM/yy" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out matchDate))
                    {
                        if (!DateOnly.TryParse(dateStr, out matchDate))
                        {
                            continue;
                        }
                    }

                    // Find team IDs
                    var homeTeam = teams.FirstOrDefault(t => t.Country == league.Country && (t.Name.Equals(rawHome, StringComparison.OrdinalIgnoreCase) || (t.FdName != null && t.FdName.Equals(rawHome, StringComparison.OrdinalIgnoreCase))));
                    var awayTeam = teams.FirstOrDefault(t => t.Country == league.Country && (t.Name.Equals(rawAway, StringComparison.OrdinalIgnoreCase) || (t.FdName != null && t.FdName.Equals(rawAway, StringComparison.OrdinalIgnoreCase))));

                    if (homeTeam == null || awayTeam == null)
                    {
                        continue;
                    }

                    var fixture = await _db.Fixtures
                        .FirstOrDefaultAsync(f => f.HomeTeamId == homeTeam.Id && f.AwayTeamId == awayTeam.Id && f.MatchDate == matchDate, ct);

                    if (fixture != null)
                    {
                        fixture.HomeGoals = fthg;
                        fixture.AwayGoals = ftag;
                        fixture.Result = ftrStr ?? (fthg > ftag ? "H" : fthg < ftag ? "A" : "D");
                        fixture.Status = "completed";
                        fixture.UpdatedAt = DateTime.UtcNow;
                        fixturesUpdated++;
                    }
                }

                await _db.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to download results for league {League}", league.Name);
            }
        }

        // 2. Settle pending bets
        var pendingBets = await _db.ValueBets
            .Include(b => b.Fixture)
            .Where(b => b.Status == "pending" && b.Fixture.Status == "completed")
            .ToListAsync(ct);

        foreach (var bet in pendingBets)
        {
            var fix = bet.Fixture;
            if (!fix.HomeGoals.HasValue || !fix.AwayGoals.HasValue)
            {
                continue;
            }

            int hg = fix.HomeGoals.Value;
            int ag = fix.AwayGoals.Value;
            bool won = false;

            switch (bet.Market)
            {
                case "1X2":
                    if (hg > ag && bet.Selection == "H")
                    {
                        won = true;
                    }
                    else if (hg == ag && bet.Selection == "D")
                    {
                        won = true;
                    }
                    else if (hg < ag && bet.Selection == "A")
                    {
                        won = true;
                    }

                    break;

                case "DC":
                    if (hg >= ag && bet.Selection == "1X")
                    {
                        won = true;
                    }
                    else if (hg <= ag && bet.Selection == "X2")
                    {
                        won = true;
                    }
                    else if (hg != ag && bet.Selection == "12")
                    {
                        won = true;
                    }

                    break;

                case "OU25":
                    int total25 = hg + ag;
                    if (bet.Selection == "Over" && total25 > 2.5)
                    {
                        won = true;
                    }
                    else if (bet.Selection == "Under" && total25 < 2.5)
                    {
                        won = true;
                    }

                    break;

                case "OU15":
                    int total15 = hg + ag;
                    if (bet.Selection == "Over" && total15 > 1.5)
                    {
                        won = true;
                    }
                    else if (bet.Selection == "Under" && total15 < 1.5)
                    {
                        won = true;
                    }

                    break;
            }

            decimal stake = bet.RecommendedStake ?? 1.0m;
            decimal price = bet.OddsPrice;

            bet.Status = won ? "won" : "lost";
            bet.Pnl = won ? Math.Round(stake * (price - 1.0m), 2) : -stake;
            bet.ActualResult = $"{hg}-{ag}";
            bet.SettledAt = DateTime.UtcNow;
            betsSettled++;
        }

        await _db.SaveChangesAsync(ct);

        return new SyncResultDto(
            Success: true,
            Message: $"Resultados sincronizados: {fixturesUpdated} jogos atualizados e {betsSettled} apostas liquidadas!",
            FixturesUpdated: fixturesUpdated,
            BetsSettled: betsSettled
        );
    }

    public async Task<SyncResultDto> SyncLiveOddsAndPredictAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Starting SyncLiveOddsAndPredictAsync...");

        var apiKey = _config["TheOddsApi:ApiKey"]
                     ?? Environment.GetEnvironmentVariable("ODDS_API_KEY")
                     ?? string.Empty;

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return new SyncResultDto(
                Success: false,
                Message: "ODDS_API_KEY não configurada em appsettings.json ou variáveis de ambiente."
            );
        }

        int fixturesAdded = 0;
        int oddsAdded = 0;
        int valueBetsCount = 0;

        var leagues = await _db.Leagues
            .Where(l => l.IsActive && !string.IsNullOrEmpty(l.FdCsvCode))
            .ToListAsync(ct);

        var teams = await _db.Teams.ToListAsync(ct);
        var bookmakers = await _db.Bookmakers.ToListAsync(ct);

        // Ensure Pinnacle bookmaker exists
        var pinnacle = bookmakers.FirstOrDefault(b => b.Code.ToLower() == "pinnacle");
        if (pinnacle == null)
        {
            pinnacle = new Bookmaker { Name = "Pinnacle", Code = "pinnacle", IsSharp = true };
            _db.Bookmakers.Add(pinnacle);
            await _db.SaveChangesAsync(ct);
            bookmakers.Add(pinnacle);
        }

        // Fetch events for each league from The Odds API
        foreach (var league in leagues)
        {
            if (!OddsApiSportKeys.TryGetValue(league.FdCsvCode!, out var sportKey))
            {
                continue;
            }

            var season = await _db.Seasons
                .FirstOrDefaultAsync(s => s.LeagueId == league.Id && s.IsCurrent, ct);
            if (season == null)
            {
                continue;
            }

            var url = $"https://api.the-odds-api.com/v4/sports/{sportKey}/odds/?regions=eu&markets=h2h,totals&oddsFormat=decimal&apiKey={apiKey}";

            try
            {
                var response = await _http.GetAsync(url, ct);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("The Odds API returned {Status} for {SportKey}", response.StatusCode, sportKey);
                    continue;
                }

                var json = await response.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(json);

                foreach (var ev in doc.RootElement.EnumerateArray())
                {
                    var homeTeamName = ev.GetProperty("home_team").GetString();
                    var awayTeamName = ev.GetProperty("away_team").GetString();
                    var commenceTimeStr = ev.GetProperty("commence_time").GetString();

                    if (string.IsNullOrEmpty(homeTeamName) || string.IsNullOrEmpty(awayTeamName) || string.IsNullOrEmpty(commenceTimeStr))
                    {
                        continue;
                    }

                    // Match teams with canonical DB
                    var homeTeam = teams.FirstOrDefault(t => t.Country == league.Country && (t.Name.Equals(homeTeamName, StringComparison.OrdinalIgnoreCase) || homeTeamName.Contains(t.Name, StringComparison.OrdinalIgnoreCase)));
                    var awayTeam = teams.FirstOrDefault(t => t.Country == league.Country && (t.Name.Equals(awayTeamName, StringComparison.OrdinalIgnoreCase) || awayTeamName.Contains(t.Name, StringComparison.OrdinalIgnoreCase)));

                    if (homeTeam == null || awayTeam == null || homeTeam.Id == awayTeam.Id)
                    {
                        continue;
                    }

                    var commenceDt = DateTime.Parse(commenceTimeStr, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);
                    var matchDate = DateOnly.FromDateTime(commenceDt);
                    var kickOff = TimeOnly.FromDateTime(commenceDt);

                    var fixture = await _db.Fixtures
                        .FirstOrDefaultAsync(f => f.HomeTeamId == homeTeam.Id && f.AwayTeamId == awayTeam.Id && f.MatchDate == matchDate, ct);

                    if (fixture == null)
                    {
                        fixture = new Fixture
                        {
                            SeasonId = season.Id,
                            HomeTeamId = homeTeam.Id,
                            AwayTeamId = awayTeam.Id,
                            MatchDate = matchDate,
                            KickOff = kickOff,
                            Status = "scheduled"
                        };
                        _db.Fixtures.Add(fixture);
                        await _db.SaveChangesAsync(ct);
                        fixturesAdded++;
                    }

                    // Process odds
                    if (ev.TryGetProperty("bookmakers", out var bks))
                    {
                        foreach (var bk in bks.EnumerateArray())
                        {
                            var bkCode = bk.GetProperty("key").GetString() ?? "";
                            var bkTitle = bk.GetProperty("title").GetString() ?? bkCode;

                            var bookie = bookmakers.FirstOrDefault(b => b.Code.Equals(bkCode, StringComparison.OrdinalIgnoreCase) || b.Name.Equals(bkTitle, StringComparison.OrdinalIgnoreCase));
                            if (bookie == null)
                            {
                                bookie = new Bookmaker { Name = bkTitle, Code = bkCode, IsSharp = bkCode.Equals("pinnacle", StringComparison.OrdinalIgnoreCase) };
                                _db.Bookmakers.Add(bookie);
                                await _db.SaveChangesAsync(ct);
                                bookmakers.Add(bookie);
                            }

                            if (bk.TryGetProperty("markets", out var markets))
                            {
                                foreach (var m in markets.EnumerateArray())
                                {
                                    var mKey = m.GetProperty("key").GetString();
                                    foreach (var outcome in m.GetProperty("outcomes").EnumerateArray())
                                    {
                                        string? marketType = null;
                                        string? selection = null;
                                        var oName = outcome.GetProperty("name").GetString();
                                        var price = outcome.GetProperty("price").GetDecimal();

                                        if (mKey == "h2h")
                                        {
                                            marketType = "1X2";
                                            if (oName == homeTeamName)
                                            {
                                                selection = "H";
                                            }
                                            else if (oName == awayTeamName)
                                            {
                                                selection = "A";
                                            }
                                            else if (oName == "Draw")
                                            {
                                                selection = "D";
                                            }
                                        }
                                        else if (mKey == "totals" && outcome.TryGetProperty("point", out var pt))
                                        {
                                            var ptVal = pt.GetDouble();
                                            if (Math.Abs(ptVal - 2.5) < 0.01)
                                            {
                                                marketType = "OU25";
                                            }
                                            else if (Math.Abs(ptVal - 1.5) < 0.01)
                                            {
                                                marketType = "OU15";
                                            }

                                            selection = oName;
                                        }

                                        if (marketType != null && selection != null && price > 1.0m)
                                        {
                                            var existingOdd = await _db.Odds
                                                .FirstOrDefaultAsync(o => o.FixtureId == fixture.Id && o.BookmakerId == bookie.Id && o.Market == marketType && o.Selection == selection, ct);

                                            if (existingOdd != null)
                                            {
                                                existingOdd.Price = price;
                                                existingOdd.CapturedAt = DateTime.UtcNow;
                                            }
                                            else
                                            {
                                                _db.Odds.Add(new Odds
                                                {
                                                    FixtureId = fixture.Id,
                                                    BookmakerId = bookie.Id,
                                                    Market = marketType,
                                                    Selection = selection,
                                                    Price = price,
                                                    OddsType = "current"
                                                });
                                                oddsAdded++;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                await _db.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching live odds for {League}", league.Name);
                _db.ChangeTracker.Clear();
            }
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // 3. Run Dixon-Coles Predictor & EV Calculator for each league
        foreach (var league in leagues)
        {
            var rawMatches = await _db.Fixtures
                .AsNoTracking()
                .Where(f => f.Season.LeagueId == league.Id && f.Status == "completed" && f.HomeXg.HasValue && f.AwayXg.HasValue)
                .Select(f => new
                {
                    HomeTeamId = f.HomeTeamId,
                    AwayTeamId = f.AwayTeamId,
                    HomeXg = f.HomeXg!.Value,
                    AwayXg = f.AwayXg!.Value,
                    MatchDate = f.MatchDate,
                    HomeGoals = f.HomeGoals,
                    AwayGoals = f.AwayGoals
                })
                .ToListAsync(ct);

            var completedMatches = rawMatches
                .Select(f => new MatchTrainingRecord(
                    f.HomeTeamId.ToString(),
                    f.AwayTeamId.ToString(),
                    (double)f.HomeXg,
                    (double)f.AwayXg,
                    (double)(today.DayNumber - f.MatchDate.DayNumber),
                    f.HomeGoals,
                    f.AwayGoals
                ))
                .ToList();

            if (completedMatches.Count < 30)
            {
                continue;
            }

            var engine = new DixonColesEngine(decayRate: 0.0065);
            engine.Fit(completedMatches);

            var scheduledFixtures = await _db.Fixtures
                .Where(f => f.Season.LeagueId == league.Id && f.Status == "scheduled")
                .ToListAsync(ct);

            foreach (var fix in scheduledFixtures)
            {
                try
                {
                    var predResult = engine.PredictMatch(fix.HomeTeamId.ToString(), fix.AwayTeamId.ToString());

                    var existingPred = await _db.ModelPredictions
                        .FirstOrDefaultAsync(p => p.FixtureId == fix.Id && p.ModelVersion == "v1_dixon_coles", ct);

                    var probs = predResult.Probabilities;
                    if (existingPred == null)
                    {
                        existingPred = new ModelPrediction
                        {
                            FixtureId = fix.Id,
                            ModelVersion = "v1_dixon_coles",
                            HomeWinProb = (decimal)probs.HomeWin,
                            DrawProb = (decimal)probs.Draw,
                            AwayWinProb = (decimal)probs.AwayWin,
                            Dc1XProb = (decimal)probs.Dc1X,
                            Dc12Prob = (decimal)probs.Dc12,
                            DcX2Prob = (decimal)probs.DcX2,
                            Over15Prob = (decimal)probs.Over15,
                            Over25Prob = (decimal)probs.Over25,
                            PredictedHomeGoals = (decimal)predResult.PredictedHomeXg,
                            PredictedAwayGoals = (decimal)predResult.PredictedAwayXg
                        };
                        _db.ModelPredictions.Add(existingPred);
                    }
                    else
                    {
                        existingPred.HomeWinProb = (decimal)probs.HomeWin;
                        existingPred.DrawProb = (decimal)probs.Draw;
                        existingPred.AwayWinProb = (decimal)probs.AwayWin;
                        existingPred.Dc1XProb = (decimal)probs.Dc1X;
                        existingPred.Dc12Prob = (decimal)probs.Dc12;
                        existingPred.DcX2Prob = (decimal)probs.DcX2;
                        existingPred.Over15Prob = (decimal)probs.Over15;
                        existingPred.Over25Prob = (decimal)probs.Over25;
                    }
                    await _db.SaveChangesAsync(ct);

                    // Now calculate EV against soft bookies and Pinnacle
                    var oddsList = await _db.Odds
                        .Include(o => o.Bookmaker)
                        .Where(o => o.FixtureId == fix.Id)
                        .ToListAsync(ct);

                    var pinOdd = oddsList.FirstOrDefault(o => o.Bookmaker.Code.ToLower() == "pinnacle");

                    foreach (var odd in oddsList)
                    {
                        if (odd.Bookmaker.IsSharp)
                        {
                            continue;
                        }

                        double modelProb = 0.0;
                        if (odd.Market == "1X2")
                        {
                            if (odd.Selection == "H")
                            {
                                modelProb = probs.HomeWin;
                            }
                            else if (odd.Selection == "D")
                            {
                                modelProb = probs.Draw;
                            }
                            else if (odd.Selection == "A")
                            {
                                modelProb = probs.AwayWin;
                            }
                        }
                        else if (odd.Market == "OU25")
                        {
                            if (odd.Selection == "Over")
                            {
                                modelProb = probs.Over25;
                            }
                            else if (odd.Selection == "Under")
                            {
                                modelProb = probs.Under25;
                            }
                        }
                        else if (odd.Market == "OU15")
                        {
                            if (odd.Selection == "Over")
                            {
                                modelProb = probs.Over15;
                            }
                            else if (odd.Selection == "Under")
                            {
                                modelProb = probs.Under15;
                            }
                        }

                        if (modelProb <= 0.0)
                        {
                            continue;
                        }

                        double? pinPrice = pinOdd != null && pinOdd.Market == odd.Market && pinOdd.Selection == odd.Selection
                            ? (double)pinOdd.Price
                            : null;

                        if (!KellyCalculator.IsPinnacleSharpLineBeaten((double)odd.Price, pinPrice))
                        {
                            continue;
                        }

                        var ev = KellyCalculator.CalculateEv(modelProb, (double)odd.Price);
                        if (ev > 3.0) // Minimum 3% EV threshold
                        {
                            var kelly = KellyCalculator.CalculateQuarterKelly(modelProb, (double)odd.Price);
                            var edge = KellyCalculator.CalculateEdge(modelProb, (double)odd.Price);

                            var vb = await _db.ValueBets
                                .FirstOrDefaultAsync(v => v.FixtureId == fix.Id && v.BookmakerId == odd.BookmakerId && v.Market == odd.Market && v.Selection == odd.Selection, ct);

                            if (vb == null)
                            {
                                vb = new ValueBet
                                {
                                    FixtureId = fix.Id,
                                    PredictionId = existingPred.Id,
                                    BookmakerId = odd.BookmakerId,
                                    Market = odd.Market,
                                    Selection = odd.Selection,
                                    ModelProb = (decimal)modelProb,
                                    OddsPrice = odd.Price,
                                    PinnaclePrice = pinPrice.HasValue ? (decimal)pinPrice.Value : null,
                                    ImpliedProb = Math.Round(1.0m / odd.Price, 4),
                                    EdgePct = (decimal)edge,
                                    EvPct = (decimal)ev,
                                    KellyFraction = (decimal)kelly,
                                    RecommendedStake = (decimal)kelly,
                                    Status = "pending"
                                };
                                _db.ValueBets.Add(vb);
                                valueBetsCount++;
                            }
                            else
                            {
                                vb.ModelProb = (decimal)modelProb;
                                vb.OddsPrice = odd.Price;
                                vb.PinnaclePrice = pinPrice.HasValue ? (decimal)pinPrice.Value : null;
                                vb.EvPct = (decimal)ev;
                                vb.KellyFraction = (decimal)kelly;
                            }
                        }
                    }
                    await _db.SaveChangesAsync(ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not predict fixture {FixtureId}", fix.Id);
                }
            }
        }

        return new SyncResultDto(
            Success: true,
            Message: $"Sincronismo concluído: {fixturesAdded} novos jogos, {oddsAdded} cotações atualizadas e {valueBetsCount} apostas +EV identificadas!",
            FixturesUpdated: fixturesAdded,
            OddsUpdated: oddsAdded,
            ValueBetsGenerated: valueBetsCount
        );
    }
}