namespace UMS.Modules.Identity.Domain.Common;

/// <summary>
/// Marker for one of the events named in requirement-spec.md identity §3
/// (<c>&lt;Entity&gt;&lt;PastTenseVerb&gt;</c>, ums-conventions.md Naming). Every one is raised
/// either from inside the aggregate whose invariant produced it (via <see cref="AggregateRoot{TId}.Raise"/>)
/// or, for an event that can occur without a successfully-loaded aggregate (e.g.
/// <c>UserLoginFailed</c> against an unknown identifier), constructed directly by the application
/// service and handed to the same dispatcher.
/// </summary>
public interface IDomainEvent
{
    public DateTimeOffset OccurredAt { get; }
}
