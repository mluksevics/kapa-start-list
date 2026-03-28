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
    int ClassId,
    string ClassName,
    int ClubId,
    string ClubName,
    string? Country,
    int StatusId,
    int StartPlace,
    string? StartTime
);

public sealed record BulkUploadResponse(
    DateOnly CompetitionDate,
    bool CompetitionCreated,
    int Inserted,
    int Updated,
    int Unchanged,
    int SkippedAsOlder
);
