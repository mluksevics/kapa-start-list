namespace StartRef.Api.Models;

// ── Partial update (PATCH /api/competitions/{date}/runners/{startNumber}) ───

public sealed record PatchRunnerRequest(
    int? StatusId,
    string? SiChipNo,
    string? Name,
    string? Surname,
    int? ClassId,
    int? ClubId,
    string? StartTime,
    DateTimeOffset LastModifiedUtc,
    string Source
);

public sealed record PatchRunnerResponse(
    int StartNumber,
    bool Applied,
    string? Reason,
    DateTimeOffset? ServerLastModifiedUtc
);
