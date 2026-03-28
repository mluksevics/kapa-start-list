namespace StartRef.Api.Models;

// ── Partial update (PATCH /api/competitions/{date}/runners/{startNumber}) ───

public sealed record PatchRunnerRequest(
    int? StatusId,
    string? SiChipNo,
    string? Name,
    string? Surname,
    string? ClubName,
    string? Country,
    int? StartPlace,
    DateTimeOffset LastModifiedUtc,
    string Source
);

public sealed record PatchRunnerResponse(
    int StartNumber,
    bool Applied,
    string? Reason,
    DateTimeOffset? ServerLastModifiedUtc
);
