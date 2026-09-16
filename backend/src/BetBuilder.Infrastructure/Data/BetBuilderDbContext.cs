using BetBuilder.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BetBuilder.Infrastructure.Data;

public class BetBuilderDbContext : DbContext
{
    public BetBuilderDbContext(DbContextOptions<BetBuilderDbContext> options) : base(options)
    {
    }

    public DbSet<League> Leagues => Set<League>();
    public DbSet<Season> Seasons => Set<Season>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<Bookmaker> Bookmakers => Set<Bookmaker>();
    public DbSet<Fixture> Fixtures => Set<Fixture>();
    public DbSet<Odds> Odds => Set<Odds>();
    public DbSet<ModelPrediction> ModelPredictions => Set<ModelPrediction>();
    public DbSet<ValueBet> ValueBets => Set<ValueBet>();
    public DbSet<LadderChallenge> LadderChallenges => Set<LadderChallenge>();
    public DbSet<LadderStep> LadderSteps => Set<LadderStep>();
    public DbSet<LadderLeg> LadderLegs => Set<LadderLeg>();
    public DbSet<BacktestResult> BacktestResults => Set<BacktestResult>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply snake_case naming conventions for PostgreSQL matching existing Supabase schema
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            var tableName = ToSnakeCase(entity.ClrType.Name);
            // Pluralize/fix table names to match Supabase exactly
            tableName = tableName switch
            {
                "league" => "leagues",
                "season" => "seasons",
                "team" => "teams",
                "bookmaker" => "bookmakers",
                "fixture" => "fixtures",
                "odds" => "odds",
                "model_prediction" => "model_predictions",
                "value_bet" => "value_bets",
                "ladder_challenge" => "ladder_challenges",
                "ladder_step" => "ladder_steps",
                "ladder_leg" => "ladder_legs",
                "backtest_result" => "backtest_results",
                _ => tableName
            };
            entity.SetTableName(tableName);

            foreach (var property in entity.GetProperties())
            {
                property.SetColumnName(ToSnakeCase(property.Name));
            }
        }

        // Configure relationships, indexes & specific constraints
        modelBuilder.Entity<League>(entity =>
        {
            entity.HasIndex(l => l.Code).IsUnique();
        });

        modelBuilder.Entity<Season>(entity =>
        {
            entity.HasIndex(s => new { s.LeagueId, s.StartYear }).IsUnique();

            entity.HasOne(s => s.League)
                .WithMany(l => l.Seasons)
                .HasForeignKey(s => s.LeagueId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Team>(entity =>
        {
            entity.HasIndex(t => new { t.Name, t.Country }).IsUnique();
        });

        modelBuilder.Entity<Bookmaker>(entity =>
        {
            entity.HasIndex(b => b.Name).IsUnique();
            entity.HasIndex(b => b.Code).IsUnique();
        });

        modelBuilder.Entity<Fixture>(entity =>
        {
            entity.HasIndex(f => new { f.HomeTeamId, f.AwayTeamId, f.MatchDate }).IsUnique();
            entity.HasIndex(f => f.SeasonId).HasDatabaseName("idx_fixtures_season");
            entity.HasIndex(f => f.MatchDate).HasDatabaseName("idx_fixtures_date");
            entity.HasIndex(f => f.Status).HasDatabaseName("idx_fixtures_status");

            entity.HasOne(f => f.Season)
                .WithMany(s => s.Fixtures)
                .HasForeignKey(f => f.SeasonId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(f => f.HomeTeam)
                .WithMany(t => t.HomeFixtures)
                .HasForeignKey(f => f.HomeTeamId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(f => f.AwayTeam)
                .WithMany(t => t.AwayFixtures)
                .HasForeignKey(f => f.AwayTeamId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Odds>(entity =>
        {
            entity.HasIndex(o => new { o.FixtureId, o.BookmakerId, o.Market, o.Selection, o.OddsType }).IsUnique();
            entity.HasIndex(o => o.FixtureId).HasDatabaseName("idx_odds_fixture");
            entity.HasIndex(o => new { o.Market, o.Selection }).HasDatabaseName("idx_odds_market");

            entity.HasOne(o => o.Fixture)
                .WithMany(f => f.Odds)
                .HasForeignKey(o => o.FixtureId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(o => o.Bookmaker)
                .WithMany(b => b.Odds)
                .HasForeignKey(o => o.BookmakerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ModelPrediction>(entity =>
        {
            entity.HasIndex(p => new { p.FixtureId, p.ModelVersion }).IsUnique();

            entity.Property(p => p.Dc1XProb).HasColumnName("dc_1x_prob");
            entity.Property(p => p.Dc12Prob).HasColumnName("dc_12_prob");
            entity.Property(p => p.DcX2Prob).HasColumnName("dc_x2_prob");
            entity.Property(p => p.Over15Prob).HasColumnName("over_15_prob");
            entity.Property(p => p.Over25Prob).HasColumnName("over_25_prob");

            entity.HasOne(p => p.Fixture)
                .WithMany(f => f.ModelPredictions)
                .HasForeignKey(p => p.FixtureId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ValueBet>(entity =>
        {
            entity.HasIndex(vb => new { vb.FixtureId, vb.BookmakerId, vb.Market, vb.Selection }).IsUnique();
            entity.HasIndex(vb => vb.Status).HasDatabaseName("idx_value_bets_status");
            entity.HasIndex(vb => vb.CreatedAt).HasDatabaseName("idx_value_bets_created");

            entity.HasOne(vb => vb.Fixture)
                .WithMany(f => f.ValueBets)
                .HasForeignKey(vb => vb.FixtureId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(vb => vb.Prediction)
                .WithMany()
                .HasForeignKey(vb => vb.PredictionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(vb => vb.Bookmaker)
                .WithMany()
                .HasForeignKey(vb => vb.BookmakerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<LadderChallenge>(entity =>
        {
            entity.HasIndex(c => c.ChallengeNumber).IsUnique();
        });

        modelBuilder.Entity<LadderStep>(entity =>
        {
            entity.HasIndex(ls => new { ls.ChallengeId, ls.StepNumber }).IsUnique();
            entity.HasIndex(ls => ls.ChallengeId).HasDatabaseName("idx_ladder_steps_challenge");

            entity.HasOne(ls => ls.Challenge)
                .WithMany(c => c.Steps)
                .HasForeignKey(ls => ls.ChallengeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LadderLeg>(entity =>
        {
            entity.HasIndex(ll => new { ll.StepId, ll.FixtureId, ll.Market, ll.Selection }).IsUnique();
            entity.HasIndex(ll => ll.StepId).HasDatabaseName("idx_ladder_legs_step");

            entity.HasOne(ll => ll.Step)
                .WithMany(s => s.Legs)
                .HasForeignKey(ll => ll.StepId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(ll => ll.Fixture)
                .WithMany()
                .HasForeignKey(ll => ll.FixtureId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<BacktestResult>(entity =>
        {
            entity.HasIndex(br => new { br.FixtureId, br.Market, br.Selection }).IsUnique();
            entity.HasIndex(br => br.FixtureId).HasDatabaseName("idx_backtest_fixture");

            entity.HasOne(br => br.Fixture)
                .WithMany()
                .HasForeignKey(br => br.FixtureId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(br => br.Bookmaker)
                .WithMany()
                .HasForeignKey(br => br.BookmakerId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static string ToSnakeCase(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];
            if (char.IsUpper(c))
            {
                if (i > 0) sb.Append('_');
                sb.Append(char.ToLowerInvariant(c));
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }
}
