namespace StartRef.Api.Models;

public record LookupItemRequest(
    int Id,
    string Name);

public record UpsertLookupRequest(
    string Source,
    DateTimeOffset LastModifiedUtc,
    IReadOnlyList<LookupItemRequest> Items);

public record UpsertLookupResponse(
    int Inserted,
    int Updated,
    int Unchanged);
