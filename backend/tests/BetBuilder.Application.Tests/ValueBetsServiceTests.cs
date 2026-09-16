using BetBuilder.Application.Services;
using BetBuilder.Domain.Entities;
using BetBuilder.Infrastructure.Data;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BetBuilder.Application.Tests;

public class ValueBetsServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BetBuilderDbContext _db;
    private readonly ValueBetsService _sut;

    private readonly League _leaguePremierLeague;
    private readonly League _leagueLaLiga;
    private readonly Season _seasonEpl;
    private readonly Season _seasonLaLiga;
    private readonly Bookmaker _bookmakerBet365;
    private readonly Bookmaker _bookmakerPinnacle;

    public ValueBetsServiceTests()
    {
        System.Globalization.CultureInfo.DefaultThreadCurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
        System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = System.Globalization.CultureInfo.InvariantCulture;

        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<BetBuilderDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new BetBuilderDbContext(options);
        _db.Database.EnsureCreated();

        // Seed base leagues & seasons
        _leaguePremierLeague = new League { Id = Guid.NewGuid(), Name = "Premier League", Code = "PL", Country = "England" };
        _leagueLaLiga = new League { Id = Guid.NewGuid(), Name = "La Liga", Code = "LL", Country = "Spain" };
        _db.Leagues.AddRange(_leaguePremierLeague, _leagueLaLiga);

        _seasonEpl = new Season { Id = Guid.NewGuid(), LeagueId = _leaguePremierLeague.Id, StartYear = 2025, EndYear = 2026, IsCurrent = true };
        _seasonLaLiga = new Season { Id = Guid.NewGuid(), LeagueId = _leagueLaLiga.Id, StartYear = 2025, EndYear = 2026, IsCurrent = true };
        _db.Seasons.AddRange(_seasonEpl, _seasonLaLiga);

        _bookmakerBet365 = new Bookmaker { Id = Guid.NewGuid(), Name = "Bet365", Code = "bet365", IsSharp = false };
        _bookmakerPinnacle = new Bookmaker { Id = Guid.NewGuid(), Name = "Pinnacle", Code = "pinnacle", IsSharp = true };
        _db.Bookmakers.AddRange(_bookmakerBet365, _bookmakerPinnacle);

        _db.SaveChanges();

        _sut = new ValueBetsService(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private Team CreateTeam(string name, string country = "England")
    {
        var team = new Team
        {
            Id = Guid.NewGuid(),
            Name = $"{name}_{Guid.NewGuid():N}",
            Country = country
        };
        _db.Teams.Add(team);
        return team;
    }

    private Fixture CreateFixture(
        Season? season = null,
        Team? homeTeam = null,
        Team? awayTeam = null,
        DateOnly? matchDate = null,
        string status = "scheduled",
        TimeOnly? kickOff = null)
    {
        var home = homeTeam ?? CreateTeam("Home");
        var away = awayTeam ?? CreateTeam("Away");

        var fixture = new Fixture
        {
            Id = Guid.NewGuid(),
            SeasonId = season?.Id ?? _seasonEpl.Id,
            HomeTeamId = home.Id,
            AwayTeamId = away.Id,
            MatchDate = matchDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
            KickOff = kickOff,
            Status = status
        };
        _db.Fixtures.Add(fixture);
        return fixture;
    }

    private ValueBet CreateValueBet(
        Fixture fixture,
        Bookmaker? bookmaker = null,
        string market = "1X2",
        string selection = "Home",
        decimal modelProb = 0.55m,
        decimal oddsPrice = 2.10m,
        decimal evPct = 5.0m,
        decimal? kellyFraction = 2.5m)
    {
        var prediction = new ModelPrediction
        {
            Id = Guid.NewGuid(),
            FixtureId = fixture.Id,
            HomeWinProb = modelProb,
            DrawProb = 0.25m,
            AwayWinProb = 0.25m,
            ModelVersion = "dixon_coles_v1"
        };
        _db.ModelPredictions.Add(prediction);

        var valueBet = new ValueBet
        {
            Id = Guid.NewGuid(),
            FixtureId = fixture.Id,
            PredictionId = prediction.Id,
            BookmakerId = bookmaker?.Id ?? _bookmakerBet365.Id,
            Market = market,
            Selection = selection,
            ModelProb = modelProb,
            OddsPrice = oddsPrice,
            ImpliedProb = oddsPrice > 0 ? 1m / oddsPrice : 0,
            EdgePct = (modelProb * oddsPrice) - 1m,
            EvPct = evPct,
            KellyFraction = kellyFraction,
            Status = "pending"
        };
        _db.ValueBets.Add(valueBet);
        return valueBet;
    }

    [Fact]
    public async Task GetValueBetsAsync_WhenNoValueBetsExist_ReturnsEmptyList()
    {
        // Act
        var result = await _sut.GetValueBetsAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetValueBetsAsync_WhenMatchingScheduledValueBetExists_ReturnsMappedDto()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var home = new Team { Id = Guid.NewGuid(), Name = "Arsenal", Country = "England" };
        var away = new Team { Id = Guid.NewGuid(), Name = "Chelsea", Country = "England" };
        _db.Teams.AddRange(home, away);

        var fixture = CreateFixture(_seasonEpl, home, away, today, "scheduled", new TimeOnly(15, 0));
        var valueBet = CreateValueBet(fixture, _bookmakerBet365, "1X2", "Home", 0.50m, 2.20m, evPct: 10.0m, kellyFraction: 2.50m);
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.GetValueBetsAsync();

        // Assert
        result.Should().HaveCount(1);
        var dto = result[0];
        dto.Id.Should().Be(valueBet.Id);
        dto.FixtureId.Should().Be(fixture.Id);
        dto.Date.Should().Be(today.ToString("yyyy-MM-dd"));
        dto.League.Should().Be("Premier League");
        dto.Match.Should().Be("Arsenal vs Chelsea (15:00)");
        dto.Market.Should().Be("1X2");
        dto.Selection.Should().Be("Home");
        dto.ModelProb.Should().Be(0.50);
        dto.ProbPercent.Should().Be("50.0%");
        dto.TrueOdds.Should().Be("2.00");
        dto.Pinnacle.Should().Be("N/A"); // No pinnacle odd seeded
        dto.Bookmaker.Should().Be("Bet365");
        dto.Odds.Should().Be(2.20);
        dto.EdgeEv.Should().Be(10.0);
        dto.QKellyStake.Should().Be("2.50%");
    }

    [Theory]
    [InlineData("completed")]
    [InlineData("postponed")]
    [InlineData("cancelled")]
    public async Task GetValueBetsAsync_ExcludesNonScheduledFixtures(string nonScheduledStatus)
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var fixtureNonScheduled = CreateFixture(status: nonScheduledStatus, matchDate: today);
        CreateValueBet(fixtureNonScheduled, _bookmakerBet365, evPct: 5.0m);

        var fixtureScheduled = CreateFixture(status: "scheduled", matchDate: today);
        CreateValueBet(fixtureScheduled, _bookmakerBet365, evPct: 5.0m);

        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.GetValueBetsAsync();

        // Assert
        result.Should().HaveCount(1);
        result[0].FixtureId.Should().Be(fixtureScheduled.Id);
    }

    [Fact]
    public async Task GetValueBetsAsync_ExcludesPastMatches_IncludesTodayAndFuture()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var pastFixture = CreateFixture(matchDate: today.AddDays(-1), status: "scheduled");
        CreateValueBet(pastFixture, _bookmakerBet365, evPct: 5.0m);

        var todayFixture = CreateFixture(matchDate: today, status: "scheduled");
        CreateValueBet(todayFixture, _bookmakerBet365, evPct: 5.0m);

        var futureFixture = CreateFixture(matchDate: today.AddDays(2), status: "scheduled");
        CreateValueBet(futureFixture, _bookmakerBet365, evPct: 5.0m);

        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.GetValueBetsAsync();

        // Assert
        result.Should().HaveCount(2);
        result.Select(x => x.FixtureId).Should().Contain(new[] { todayFixture.Id, futureFixture.Id });
        result.Select(x => x.FixtureId).Should().NotContain(pastFixture.Id);
    }

    [Fact]
    public async Task GetValueBetsAsync_FiltersByMinEdge()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var fix1 = CreateFixture(matchDate: today, status: "scheduled");
        CreateValueBet(fix1, _bookmakerBet365, evPct: 1.5m);

        var fix2 = CreateFixture(matchDate: today.AddDays(1), status: "scheduled");
        CreateValueBet(fix2, _bookmakerBet365, evPct: 2.0m);

        var fix3 = CreateFixture(matchDate: today.AddDays(2), status: "scheduled");
        CreateValueBet(fix3, _bookmakerBet365, evPct: 5.0m);

        await _db.SaveChangesAsync();

        // Act 1: default minEdge (2.0)
        var defaultResult = await _sut.GetValueBetsAsync();
        defaultResult.Should().HaveCount(2);
        defaultResult.Select(x => x.FixtureId).Should().Contain(new[] { fix2.Id, fix3.Id });

        // Act 2: custom minEdge = 3.0
        var customResult = await _sut.GetValueBetsAsync(minEdge: 3.0);
        customResult.Should().HaveCount(1);
        customResult[0].FixtureId.Should().Be(fix3.Id);

        // Act 3: lower minEdge = 1.0
        var lowerResult = await _sut.GetValueBetsAsync(minEdge: 1.0);
        lowerResult.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetValueBetsAsync_FiltersByLeagueId_WhenSpecified()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var eplFixture = CreateFixture(season: _seasonEpl, matchDate: today, status: "scheduled");
        CreateValueBet(eplFixture, _bookmakerBet365, evPct: 4.0m);

        var laLigaFixture = CreateFixture(season: _seasonLaLiga, matchDate: today, status: "scheduled");
        CreateValueBet(laLigaFixture, _bookmakerBet365, evPct: 4.0m);

        await _db.SaveChangesAsync();

        // Act 1: Filter by Premier League
        var eplOnly = await _sut.GetValueBetsAsync(leagueId: _leaguePremierLeague.Id);
        eplOnly.Should().HaveCount(1);
        eplOnly[0].League.Should().Be("Premier League");

        // Act 2: Filter by La Liga
        var laLigaOnly = await _sut.GetValueBetsAsync(leagueId: _leagueLaLiga.Id);
        laLigaOnly.Should().HaveCount(1);
        laLigaOnly[0].League.Should().Be("La Liga");

        // Act 3: Null or Empty Guid returns all leagues
        var allLeaguesNull = await _sut.GetValueBetsAsync(leagueId: null);
        allLeaguesNull.Should().HaveCount(2);

        var allLeaguesEmpty = await _sut.GetValueBetsAsync(leagueId: Guid.Empty);
        allLeaguesEmpty.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetValueBetsAsync_FiltersByDate_WhenValidDateProvided()
    {
        // Arrange
        var targetDate = new DateOnly(2026, 9, 20);
        var otherDate = new DateOnly(2026, 9, 21);

        var targetFixture = CreateFixture(matchDate: targetDate, status: "scheduled");
        CreateValueBet(targetFixture, _bookmakerBet365, evPct: 3.5m);

        var otherFixture = CreateFixture(matchDate: otherDate, status: "scheduled");
        CreateValueBet(otherFixture, _bookmakerBet365, evPct: 3.5m);

        await _db.SaveChangesAsync();

        // Act 1: Valid filter date
        var targetResult = await _sut.GetValueBetsAsync(date: "2026-09-20");
        targetResult.Should().HaveCount(1);
        targetResult[0].Date.Should().Be("2026-09-20");

        // Act 2: Invalid date string is safely ignored
        var invalidDateResult = await _sut.GetValueBetsAsync(date: "invalid-date-format");
        invalidDateResult.Should().HaveCount(2);

        // Act 3: Null or whitespace date string is ignored
        var nullDateResult = await _sut.GetValueBetsAsync(date: null);
        nullDateResult.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetValueBetsAsync_OrdersByEvPctDescending()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var fix1 = CreateFixture(matchDate: today, status: "scheduled");
        CreateValueBet(fix1, _bookmakerBet365, evPct: 2.5m);

        var fix2 = CreateFixture(matchDate: today.AddDays(1), status: "scheduled");
        CreateValueBet(fix2, _bookmakerBet365, evPct: 8.5m);

        var fix3 = CreateFixture(matchDate: today.AddDays(2), status: "scheduled");
        CreateValueBet(fix3, _bookmakerBet365, evPct: 4.2m);

        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.GetValueBetsAsync();

        // Assert
        result.Should().HaveCount(3);
        result.Select(x => x.EdgeEv).Should().ContainInConsecutiveOrder(8.5, 4.2, 2.5);
    }

    [Fact]
    public async Task GetValueBetsAsync_WhenPinnacleOddsExist_MapsPinnaclePriceCorrectly()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var fixture = CreateFixture(matchDate: today, status: "scheduled");
        CreateValueBet(fixture, _bookmakerBet365, market: "1X2", selection: "Home", evPct: 5.0m);

        // Add Pinnacle sharp odd for same fixture, market and selection
        var pinOdd = new Odds
        {
            Id = Guid.NewGuid(),
            FixtureId = fixture.Id,
            BookmakerId = _bookmakerPinnacle.Id,
            Market = "1X2",
            Selection = "Home",
            Price = 2.15m,
            OddsType = "closing"
        };
        _db.Odds.Add(pinOdd);
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.GetValueBetsAsync();

        // Assert
        result.Should().HaveCount(1);
        result[0].Pinnacle.Should().Be("2.15");
    }

    [Fact]
    public async Task GetValueBetsAsync_WhenKickOffIsNull_FormatsMatchWithoutTime()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var home = new Team { Id = Guid.NewGuid(), Name = "Arsenal", Country = "England" };
        var away = new Team { Id = Guid.NewGuid(), Name = "Chelsea", Country = "England" };
        _db.Teams.AddRange(home, away);

        var fixture = CreateFixture(homeTeam: home, awayTeam: away, matchDate: today, status: "scheduled", kickOff: null);
        CreateValueBet(fixture, _bookmakerBet365, evPct: 4.0m);
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.GetValueBetsAsync();

        // Assert
        result.Should().HaveCount(1);
        result[0].Match.Should().Be("Arsenal vs Chelsea");
    }

    [Fact]
    public async Task GetValueBetsAsync_WhenKellyFractionIsNull_FormatsKellyStakeAsZero()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var fixture = CreateFixture(matchDate: today, status: "scheduled");
        CreateValueBet(fixture, _bookmakerBet365, evPct: 3.0m, kellyFraction: null);
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.GetValueBetsAsync();

        // Assert
        result.Should().HaveCount(1);
        result[0].QKellyStake.Should().Be("0.00%");
    }

    [Fact]
    public async Task GetValueBetsAsync_WhenModelProbIsZero_FormatsTrueOddsAsZero()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var fixture = CreateFixture(matchDate: today, status: "scheduled");
        CreateValueBet(fixture, _bookmakerBet365, modelProb: 0.0m, evPct: 3.0m);
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.GetValueBetsAsync();

        // Assert
        result.Should().HaveCount(1);
        result[0].TrueOdds.Should().Be("0.00");
        result[0].ProbPercent.Should().Be("0.0%");
    }

    [Fact]
    public async Task GetValueBetsAsync_RespectsCancellationToken()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = () => _sut.GetValueBetsAsync(ct: cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task GetValueBetsAsync_WhenMultiplePinnacleOddsExist_DoesNotThrowAndUsesLatestPrice()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var fixture = CreateFixture(matchDate: today, status: "scheduled");
        CreateValueBet(fixture, _bookmakerBet365, market: "1X2", selection: "Home", evPct: 5.0m);

        // Add 2 Pinnacle odds (e.g. opening and closing) for the same fixture/market/selection
        var now = DateTime.UtcNow;
        var openingOdd = new Odds
        {
            Id = Guid.NewGuid(),
            FixtureId = fixture.Id,
            BookmakerId = _bookmakerPinnacle.Id,
            Market = "1X2",
            Selection = "Home",
            Price = 2.10m,
            OddsType = "opening",
            CapturedAt = now.AddMinutes(-30)
        };
        var closingOdd = new Odds
        {
            Id = Guid.NewGuid(),
            FixtureId = fixture.Id,
            BookmakerId = _bookmakerPinnacle.Id,
            Market = "1X2",
            Selection = "Home",
            Price = 2.25m,
            OddsType = "closing",
            CapturedAt = now
        };
        _db.Odds.AddRange(openingOdd, closingOdd);
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.GetValueBetsAsync();

        // Assert
        result.Should().HaveCount(1);
        result[0].Pinnacle.Should().Be("2.25");
    }
}
