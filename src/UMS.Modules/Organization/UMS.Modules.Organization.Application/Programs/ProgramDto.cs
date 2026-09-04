namespace UMS.Modules.Organization.Application.Programs;

public sealed record ProgramDto(Guid Id, Guid DepartmentId, string Name, string LocalizedName, string Status, DateTimeOffset CreatedAt, uint Version);

public sealed record CreateProgramRequest(Guid DepartmentId, string Name, IReadOnlyDictionary<string, string>? Translations);

public sealed record UpdateProgramRequest(string Name, uint Version, IReadOnlyDictionary<string, string>? Translations);

public sealed record ProgramListPage(IReadOnlyList<ProgramDto> Items, int TotalCount, int Skip, int Take);
