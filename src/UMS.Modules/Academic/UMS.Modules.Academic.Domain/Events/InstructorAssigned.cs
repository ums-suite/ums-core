using UMS.Modules.Academic.Domain.Common;

namespace UMS.Modules.Academic.Domain.Events;

/// <summary>
/// docs/ddd/ubiquitous-language.md, `CourseAssignment`: "Written by Academic ... Faculty holds an
/// eventually-consistent projection". Payload shape (`FacultyMemberId`, `CourseOfferingId`) is a
/// fixed, load-bearing contract - it must exactly match
/// <c>UMS.Modules.Faculty.Application.CourseAssignments.InstructorAssignmentPayload</c>, which
/// Faculty's own <c>AcademicOutboxEventSource</c>/<c>CourseAssignmentProjectionService</c> (FAC-4,
/// already merged) deserialize this event's JSON payload into.
/// </summary>
public sealed record InstructorAssigned(Guid FacultyMemberId, Guid CourseOfferingId, DateTimeOffset OccurredAt) : IDomainEvent;
