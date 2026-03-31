using System.Text.Json.Serialization;

namespace StartRef.Desktop.Models;

public class BulkRunnerDto
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

    [JsonPropertyName("statusId")]
    public int StatusId { get; set; } = 1;

    [JsonPropertyName("startTime")]
    public string? StartTime { get; set; }

    // Per-runner timestamp used by API for last-write-wins comparison.
    // DBISAM does not expose modification timestamps, so this is set to scan time.
    // TODO: use actual DBISAM row timestamp once DLL exposes it.
    [JsonPropertyName("lastModifiedUtc")]
    public DateTimeOffset LastModifiedUtc { get; set; }
}
