using System.Text.Json.Serialization;

namespace StartRef.Desktop.Models;

public class LookupItemDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
}
