namespace UMS.Modules.Faculty.Application.CourseAssignments;

public sealed record CourseAssignmentDto(Guid Id, Guid FacultyMemberId, Guid CourseOfferingId, string Status, DateTimeOffset AssignedAt, DateTimeOffset? EndedAt);

/// <summary>The payload shape Faculty's consumer expects inside an inbound `InstructorAssigned`/`InstructorUnassigned` envelope (requirement-spec.md faculty §2/§6). Academic (Flow #12) will publish matching fields when it lands.</summary>
public sealed record InstructorAssignmentPayload(Guid FacultyMemberId, Guid CourseOfferingId);
