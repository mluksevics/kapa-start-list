using System.Text.Json.Serialization;

namespace StartRef.Desktop.Models;

public class UpsertLookupResponse
{
    [JsonPropertyName("inserted")]
    public int Inserted { get; set; }

    [JsonPropertyName("updated")]
    public int Updated { get; set; }

    [JsonPropertyName("unchanged")]
    public int Unchanged { get; set; }
}
