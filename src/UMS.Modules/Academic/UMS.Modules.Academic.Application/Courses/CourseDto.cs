namespace UMS.Modules.Academic.Application.Courses;

public sealed record CourseDto(Guid Id, string Code, string Title, int CreditHours, IReadOnlyCollection<Guid> Prerequisites, DateTimeOffset CreatedAt);

public sealed record CreateCourseRequest(string Code, string Title, int CreditHours, IReadOnlyCollection<Guid>? PrerequisiteCourseIds);
