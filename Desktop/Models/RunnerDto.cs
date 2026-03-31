using System.Text.Json.Serialization;

namespace StartRef.Desktop.Models;

public class RunnerDto
{
    [JsonPropertyName("startNumber")]
    public int StartNumber { get; set; }

    [JsonPropertyName("siChipNo")]
    public string? SiChipNo { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("surname")]
    public string Surname { get; set; } = "";

    [JsonPropertyName("classId")]
    public int ClassId { get; set; }

    [JsonPropertyName("className")]
    public string ClassName { get; set; } = "";

    [JsonPropertyName("clubId")]
    public int ClubId { get; set; }

    [JsonPropertyName("clubName")]
    public string ClubName { get; set; } = "";

    [JsonPropertyName("country")]
    public string? Country { get; set; }

    [JsonPropertyName("statusId")]
    public int StatusId { get; set; } = 1;

    [JsonPropertyName("statusName")]
    public string StatusName { get; set; } = "Registered";

    [JsonPropertyName("startPlace")]
    public int StartPlace { get; set; }

    [JsonPropertyName("startTime")]
    public string? StartTime { get; set; }

    [JsonPropertyName("lastModifiedUtc")]
    public DateTimeOffset LastModifiedUtc { get; set; }

    [JsonPropertyName("lastModifiedBy")]
    public string LastModifiedBy { get; set; } = "";

    [JsonPropertyName("changedFields")]
    public List<string>? ChangedFields { get; set; }
}
