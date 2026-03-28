using System.Text.Json.Serialization;

namespace StartRef.Desktop;

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

    [JsonPropertyName("className")]
    public string ClassName { get; set; } = "";

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

    [JsonPropertyName("lastModifiedUtc")]
    public DateTimeOffset LastModifiedUtc { get; set; }

    [JsonPropertyName("lastModifiedBy")]
    public string LastModifiedBy { get; set; } = "";
}

public class GetRunnersResponse
{
    [JsonPropertyName("serverTimeUtc")]
    public DateTimeOffset ServerTimeUtc { get; set; }

    [JsonPropertyName("runners")]
    public List<RunnerDto> Runners { get; set; } = [];
}

public class BulkUploadRequest
{
    [JsonPropertyName("source")]
    public string Source { get; set; } = "desktop";

    [JsonPropertyName("lastModifiedUtc")]
    public DateTimeOffset LastModifiedUtc { get; set; }

    [JsonPropertyName("runners")]
    public List<BulkRunnerDto> Runners { get; set; } = [];
}

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

    [JsonPropertyName("className")]
    public string ClassName { get; set; } = "";

    [JsonPropertyName("clubName")]
    public string ClubName { get; set; } = "";

    [JsonPropertyName("country")]
    public string? Country { get; set; }

    [JsonPropertyName("statusId")]
    public int StatusId { get; set; } = 1;

    [JsonPropertyName("startPlace")]
    public int StartPlace { get; set; }
}

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
