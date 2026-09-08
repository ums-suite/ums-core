using UMS.Modules.Reporting.Domain.Common;

namespace UMS.Modules.Reporting.Application.Abstractions;

/// <summary>Lets an application service enqueue a domain event directly. Mirrors every other module's own <c>IDomainEventRecorder</c> exactly.</summary>
public interface IDomainEventRecorder
{
    public void Enqueue(IDomainEvent domainEvent);
}
