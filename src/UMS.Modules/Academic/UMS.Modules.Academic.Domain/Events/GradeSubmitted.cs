using UMS.Modules.Academic.Domain.Common;

namespace UMS.Modules.Academic.Domain.Events;

/// <summary>ACD-10: Faculty submits marks; the aggregate Grade is calculated per the CourseOffering's configured weighting (requirement-spec.md §3).</summary>
public sealed record GradeSubmitted(Guid GradeId, Guid EnrollmentId, Guid CourseOfferingId, decimal CalculatedScore, Guid SubmittedByUserId, DateTimeOffset OccurredAt) : IDomainEvent;
