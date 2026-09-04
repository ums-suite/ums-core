namespace UMS.Modules.Organization.Application.Departments;

public sealed record DepartmentDto(Guid Id, Guid FacultyId, string Name, string LocalizedName, string Status, DateTimeOffset CreatedAt, uint Version);

public sealed record CreateDepartmentRequest(Guid FacultyId, string Name, IReadOnlyDictionary<string, string>? Translations);

public sealed record UpdateDepartmentRequest(string Name, uint Version, IReadOnlyDictionary<string, string>? Translations);

public sealed record DepartmentListPage(IReadOnlyList<DepartmentDto> Items, int TotalCount, int Skip, int Take);
