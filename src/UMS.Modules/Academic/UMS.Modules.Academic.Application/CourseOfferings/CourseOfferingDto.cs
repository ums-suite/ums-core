namespace UMS.Modules.Academic.Application.CourseOfferings;

public sealed record CourseOfferingDto(
    Guid Id,
    Guid CourseId,
    Guid SemesterId,
    Guid DepartmentId,
    int Capacity,
    int EnrolledCount,
    bool HasAvailableSeats,
    Guid? InstructorFacultyMemberId,
    IReadOnlyCollection<SectionDto> Sections,
    IReadOnlyCollection<ExamDto> Exams,
    DateTimeOffset CreatedAt);

public sealed record SectionDto(Guid Id, string Code, DayOfWeek DayOfWeek, TimeOnly Start, TimeOnly End);

public sealed record AssessmentDto(Guid Id, string Name, decimal Weight);

public sealed record ExamDto(Guid Id, string Name, IReadOnlyCollection<AssessmentDto> Assessments);

public sealed record CreateSectionRequest(string Code, DayOfWeek DayOfWeek, TimeOnly Start, TimeOnly End);

public sealed record CreateCourseOfferingRequest(Guid CourseId, Guid SemesterId, Guid DepartmentId, int Capacity, IReadOnlyCollection<CreateSectionRequest> Sections);

public sealed record AssignInstructorRequest(Guid FacultyMemberId);

public sealed record CreateAssessmentRequest(string Name, decimal Weight);

public sealed record CreateExamRequest(string Name, IReadOnlyCollection<CreateAssessmentRequest> Assessments);
