using UMS.Modules.Library.Domain.Common;

namespace UMS.Modules.Library.Application.Abstractions;

/// <summary>Lets an application service enqueue a domain event directly for a write that bypasses an aggregate's own change-tracked <c>Raise</c> (e.g. LIB-9's atomic conditional-SQL reservation offer). Mirrors every other module's own <c>IDomainEventRecorder</c> exactly.</summary>
public interface IDomainEventRecorder
{
    public void Enqueue(IDomainEvent domainEvent);
}
