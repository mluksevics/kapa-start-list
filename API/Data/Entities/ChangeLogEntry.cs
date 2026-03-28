namespace StartRef.Api.Data.Entities;

public class ChangeLogEntry
{
    public long Id { get; set; }
    public DateOnly CompetitionDate { get; set; }
    public int StartNumber { get; set; }
    public string FieldName { get; set; } = "";
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public DateTimeOffset ChangedAtUtc { get; set; }
    public string ChangedBy { get; set; } = "";
}
