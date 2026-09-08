using UMS.Modules.Content.Domain.Common;

namespace UMS.Modules.Content.Application.Abstractions;

/// <summary>Lets an application service enqueue a domain event directly for a write that bypasses an aggregate's own change-tracked <c>Raise</c>. Mirrors every other module's own <c>IDomainEventRecorder</c> exactly.</summary>
public interface IDomainEventRecorder
{
    public void Enqueue(IDomainEvent domainEvent);
}
