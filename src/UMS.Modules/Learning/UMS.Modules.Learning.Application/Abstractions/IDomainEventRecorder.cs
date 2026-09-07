using UMS.Modules.Learning.Domain.Common;

namespace UMS.Modules.Learning.Application.Abstractions;

/// <summary>
/// Lets an application service enqueue a domain event directly, for a write that doesn't originate
/// from a tracked aggregate's own <c>Raise</c>. Mirrors Academic's own
/// <c>IDomainEventRecorder</c> exactly.
///
/// <para>
/// Learning has no <c>ExecuteUpdateAsync</c>-style change-tracker-bypassing write path at all -
/// design-decisions.md's "Why Submission Timing Doesn't Need Seat-Limit-Style Concurrency Control"
/// is precisely the decision that keeps it that way - so every event this module raises today comes
/// from an aggregate the DbContext is already tracking. This interface exists for symmetry with the
/// rest of the codebase and for the worker paths that record an outcome against an aggregate loaded
/// outside a request scope.
/// </para>
/// </summary>
public interface IDomainEventRecorder
{
    public void Enqueue(IDomainEvent domainEvent);
}
