using System.Text.Json.Serialization;

namespace StartRef.Desktop.Models;

public class BulkUploadResponse
{
    [JsonPropertyName("competitionDate")]
    public string CompetitionDate { get; set; } = "";

    [JsonPropertyName("competitionCreated")]
    public bool CompetitionCreated { get; set; }

    [JsonPropertyName("inserted")]
    public int Inserted { get; set; }

    [JsonPropertyName("updated")]
    public int Updated { get; set; }

    [JsonPropertyName("unchanged")]
    public int Unchanged { get; set; }

    [JsonPropertyName("skippedAsOlder")]
    public int SkippedAsOlder { get; set; }
}
