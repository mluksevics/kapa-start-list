using System.Text.Json.Serialization;

namespace StartRef.Desktop.Models;

public class LookupCountsResponse
{
    [JsonPropertyName("competitors")]
    public int Competitors { get; set; }

    [JsonPropertyName("clubs")]
    public int Clubs { get; set; }

    [JsonPropertyName("classes")]
    public int Classes { get; set; }
}
