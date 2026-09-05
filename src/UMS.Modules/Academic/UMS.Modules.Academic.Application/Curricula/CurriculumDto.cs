namespace UMS.Modules.Academic.Application.Curricula;

public sealed record CurriculumDto(Guid Id, Guid ProgramId, int Version, IReadOnlyCollection<CurriculumCourseEntryDto> Courses, DateTimeOffset CreatedAt);

public sealed record CurriculumCourseEntryDto(Guid CourseId, bool IsRequired);

public sealed record CreateCurriculumRequest(Guid ProgramId, int Version, IReadOnlyCollection<CurriculumCourseEntryDto> Courses);
