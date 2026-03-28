namespace StartRef.Api.Models;

// ── Bulk upload (PUT /api/competitions/{date}/runners) ──────────────────────

public sealed record BulkUploadRequest(
    string Source,
    DateTimeOffset LastModifiedUtc,
    List<BulkRunnerDto> Runners
);

public sealed record BulkRunnerDto(
    int StartNumber,
    string? SiChipNo,
    string Name,
    string Surname,
    string ClassName,
    string ClubName,
    string? Country,
    int StatusId,
    int StartPlace
);

public sealed record BulkUploadResponse(
    DateOnly CompetitionDate,
    bool CompetitionCreated,
    int Inserted,
    int Updated,
    int Unchanged,
    int SkippedAsOlder
);
