namespace BetBuilder.Domain.Entities;

public class Team
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? ShortName { get; set; }
    public string Country { get; set; } = string.Empty;
    public string? FdName { get; set; }
    public string? UnderstatName { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Fixture> HomeFixtures { get; set; } = new List<Fixture>();
    public ICollection<Fixture> AwayFixtures { get; set; } = new List<Fixture>();
}
