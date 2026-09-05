using UMS.Modules.Academic.Domain.Common;

namespace UMS.Modules.Academic.Domain.Events;

/// <summary>See <see cref="InstructorAssigned"/>'s own remarks - the payload shape is the identical fixed contract Faculty's FAC-4 projection already expects.</summary>
public sealed record InstructorUnassigned(Guid FacultyMemberId, Guid CourseOfferingId, DateTimeOffset OccurredAt) : IDomainEvent;
