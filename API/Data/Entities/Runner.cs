namespace StartRef.Api.Data.Entities;

public class Runner
{
    public DateOnly CompetitionDate { get; set; }
    public int StartNumber { get; set; }
    public string? SiChipNo { get; set; }
    public string Name { get; set; } = "";
    public string Surname { get; set; } = "";
    public int ClassId { get; set; }
    public int ClubId { get; set; }
    public string? Country { get; set; }
    public int StatusId { get; set; } = 1;
    public int StartPlace { get; set; }
    public TimeOnly? StartTime { get; set; }
    public DateTimeOffset LastModifiedUtc { get; set; }
    public string LastModifiedBy { get; set; } = "";

    public Status Status { get; set; } = null!;
    public Competition Competition { get; set; } = null!;
}
