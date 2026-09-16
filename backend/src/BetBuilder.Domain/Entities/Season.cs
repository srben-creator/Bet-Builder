namespace BetBuilder.Domain.Entities;

public class Season
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid LeagueId { get; set; }
    public int StartYear { get; set; }
    public int EndYear { get; set; }
    public bool IsCurrent { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public League League { get; set; } = null!;
    public ICollection<Fixture> Fixtures { get; set; } = new List<Fixture>();
}
