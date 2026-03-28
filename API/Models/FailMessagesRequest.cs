namespace StartRef.Api.Models;

// ── Runner GET response ──────────────────────────────────────────────────────

public sealed record RunnerResponse(
    DateOnly CompetitionDate,
    int StartNumber,
    string? SiChipNo,
    string Name,
    string Surname,
    string ClassName,
    string ClubName,
    string? Country,
    int StatusId,
    string StatusName,
    int StartPlace,
    DateTimeOffset LastModifiedUtc,
    string LastModifiedBy
);

public sealed record GetRunnersResponse(
    DateTimeOffset ServerTimeUtc,
    List<RunnerResponse> Runners
);
