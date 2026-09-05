namespace UMS.Modules.Academic.Application.AcademicSessions;

public sealed record AcademicSessionDto(Guid Id, string Code, IReadOnlyCollection<SemesterDto> Semesters, DateTimeOffset CreatedAt);

public sealed record SemesterDto(Guid Id, string Name, DateOnly RegistrationStart, DateOnly RegistrationEnd, DateOnly DropStart, DateOnly DropEnd);

public sealed record CreateSemesterRequest(string Name, DateOnly RegistrationStart, DateOnly RegistrationEnd, DateOnly DropStart, DateOnly DropEnd);

public sealed record CreateAcademicSessionRequest(string Code, IReadOnlyCollection<CreateSemesterRequest> Semesters);
