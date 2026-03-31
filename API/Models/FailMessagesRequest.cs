namespace StartRef.Api.Models;

// ── Runner GET response ──────────────────────────────────────────────────────

public sealed record RunnerResponse(
    DateOnly CompetitionDate,
    int StartNumber,
    string? SiChipNo,
    string Name,
    string Surname,
    int ClassId,
    string ClassName,
    int ClubId,
    string ClubName,
    int StatusId,
    string StatusName,
    string? StartTime,
    DateTimeOffset LastModifiedUtc,
    string LastModifiedBy,
    IReadOnlyList<string>? ChangedFields = null);

public sealed record GetRunnersResponse(
    DateTimeOffset ServerTimeUtc,
    List<RunnerResponse> Runners
);
