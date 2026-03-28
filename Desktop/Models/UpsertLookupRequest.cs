using System.Text.Json.Serialization;

namespace StartRef.Desktop.Models;

public class UpsertLookupRequest
{
    [JsonPropertyName("source")]
    public string Source { get; set; } = "desktop";

    [JsonPropertyName("lastModifiedUtc")]
    public DateTimeOffset LastModifiedUtc { get; set; }

    [JsonPropertyName("items")]
    public List<LookupItemDto> Items { get; set; } = [];
}
