namespace BetBuilder.Domain.Entities;

public class League
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? FdCsvCode { get; set; }
    public string? UnderstatName { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Season> Seasons { get; set; } = new List<Season>();
}
