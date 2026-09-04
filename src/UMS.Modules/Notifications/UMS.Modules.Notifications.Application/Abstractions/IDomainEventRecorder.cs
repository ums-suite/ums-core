using UMS.Modules.Notifications.Domain.Common;

namespace UMS.Modules.Notifications.Application.Abstractions;

/// <summary>Mirrors Identity's own <c>IDomainEventRecorder</c> - records an event that did not originate from a loaded aggregate's own method.</summary>
public interface IDomainEventRecorder
{
    public void Enqueue(IDomainEvent domainEvent);
}
