namespace UMS.Modules.Academic.Application.Programs;

public sealed record ProgramDto(Guid Id, Guid DepartmentId, string Code, string Name, int MaxCreditsPerSemester, bool RequiresAdvisorApproval, DateTimeOffset CreatedAt);

public sealed record CreateProgramRequest(Guid DepartmentId, string Code, string Name, int MaxCreditsPerSemester, bool RequiresAdvisorApproval);
