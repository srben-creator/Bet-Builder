using BetBuilder.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;
using FluentAssertions;

namespace BetBuilder.Application.Tests;

public class DatabaseFixtureTests
{
    [Fact]
    public void DbContext_WithSqliteInMemory_CreatesDatabaseSuccessfully()
    {
        // Arrange
        using var connection = new SqliteConnection("Filename=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<BetBuilderDbContext>()
            .UseSqlite(connection)
            .Options;

        using var db = new BetBuilderDbContext(options);

        // Act
        var created = db.Database.EnsureCreated();

        // Assert
        created.Should().BeTrue();
    }
}

