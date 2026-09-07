using UMS.Modules.Learning.Domain.Common;

namespace UMS.Modules.Learning.Domain.Events;

/// <summary>
/// LRN-11: an Instructor recorded an <c>AssignmentScore</c> (requirement-spec.md learning §3).
///
/// <para>
/// <b>A fan-out event, and only ever a fan-out event.</b> design-decisions.md's "Cross-Module Feed
/// of Assignment Scores into Academic's Grade": Academic MAY, at its own module's discretion in a
/// future revision of its own spec, subscribe to this as a read-only, non-authoritative input
/// while a human still enters the authoritative <c>Assessment</c>/<c>Grade</c>. Learning never
/// calls Academic to write anything, and Academic is never added to Learning's
/// module-boundaries.md dependency row as a caller - that is what keeps the documented acyclic
/// 18-module graph intact.
/// </para>
/// </summary>
public sealed record SubmissionEvaluated(
    Guid SubmissionId,
    Guid AssignmentId,
    Guid CourseOfferingId,
    Guid StudentId,
    Guid StudentUserId,
    decimal AwardedPoints,
    int MaxPoints,
    Guid EvaluatedByUserId,
    DateTimeOffset OccurredAt) : IDomainEvent;
