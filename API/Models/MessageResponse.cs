namespace StartRef.Api.Models;

// ── Changelog & lookup responses ─────────────────────────────────────────────

public sealed record ChangeLogEntryResponse(
    long Id,
    DateOnly CompetitionDate,
    int StartNumber,
    string FieldName,
    string? OldValue,
    string? NewValue,
    DateTimeOffset ChangedAtUtc,
    string ChangedBy
);

public sealed record StatusResponse(int Id, string Name);

public sealed record CompetitionResponse(DateOnly Date, string? Name, DateTimeOffset CreatedAtUtc);

public sealed record CompetitionRunnerCountResponse(
    DateOnly Date,
    string? Name,
    int RunnersCount
);
