namespace UMS.Modules.Organization.Application.Universities;

public sealed record UniversityDto(Guid Id, string Name, string? Code, string Status, DateTimeOffset CreatedAt, uint Version);

public sealed record CreateUniversityRequest(string Name, string? Code);

/// <summary>`PATCH /universities/{id}` covers both a rename and an activate/deactivate transition in one request body (requirement-spec.md organization §6 lists no separate deactivate endpoint for University) - `Version` is required per design-decisions.md's optimistic-concurrency decision.</summary>
public sealed record UpdateUniversityRequest(string? Name, string? Status, uint Version);

public sealed record UniversityListPage(IReadOnlyList<UniversityDto> Items, int TotalCount, int Skip, int Take);
