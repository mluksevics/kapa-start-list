using System.Text.Json.Serialization;

namespace StartRef.Desktop.Models;

public class BulkUploadRequest
{
    [JsonPropertyName("source")]
    public string Source { get; set; } = "desktop";

    [JsonPropertyName("lastModifiedUtc")]
    public DateTimeOffset LastModifiedUtc { get; set; }

    [JsonPropertyName("runners")]
    public List<BulkRunnerDto> Runners { get; set; } = [];
}
