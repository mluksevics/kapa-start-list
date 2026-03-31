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
    int StatusId,
    string? StartTime,
    // Per-runner timestamp for last-write-wins. Set to scan time by desktop;
    // TODO: use actual DBISAM row timestamp once DLL exposes it.
    DateTimeOffset LastModifiedUtc
);

public sealed record BulkUploadResponse(
    DateOnly CompetitionDate,
    bool CompetitionCreated,
    int Inserted,
    int Updated,
    int Unchanged,
    int SkippedAsOlder
);
