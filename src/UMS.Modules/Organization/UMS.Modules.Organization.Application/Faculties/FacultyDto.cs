namespace UMS.Modules.Organization.Application.Faculties;

/// <summary><paramref name="LocalizedName"/> is <see cref="Domain.Faculties.Faculty.ResolveName"/>'s result for the caller's requested language (ORG-8/ADR-0011) - equal to <paramref name="Name"/> whenever no translation row exists or English was requested.</summary>
public sealed record FacultyDto(Guid Id, Guid CampusId, string Name, string LocalizedName, string Status, DateTimeOffset CreatedAt, uint Version);

public sealed record CreateFacultyRequest(Guid CampusId, string Name, IReadOnlyDictionary<string, string>? Translations);

public sealed record UpdateFacultyRequest(string Name, uint Version, IReadOnlyDictionary<string, string>? Translations);

public sealed record FacultyListPage(IReadOnlyList<FacultyDto> Items, int TotalCount, int Skip, int Take);
