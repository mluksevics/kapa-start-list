namespace StartRef.Api.Data.Entities;

public class Competition
{
    public DateOnly Date { get; set; }
    public string? Name { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }

    public ICollection<Runner> Runners { get; set; } = [];
}
