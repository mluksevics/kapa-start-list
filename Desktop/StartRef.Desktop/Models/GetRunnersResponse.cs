using System.Text.Json.Serialization;

namespace StartRef.Desktop.Models;

public class GetRunnersResponse
{
    [JsonPropertyName("serverTimeUtc")]
    public DateTimeOffset ServerTimeUtc { get; set; }

    [JsonPropertyName("runners")]
    public List<RunnerDto> Runners { get; set; } = [];
}
