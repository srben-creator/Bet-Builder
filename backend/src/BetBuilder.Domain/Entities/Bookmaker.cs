namespace BetBuilder.Domain.Entities;

public class Bookmaker
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public bool IsSharp { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Odds> Odds { get; set; } = new List<Odds>();
}
