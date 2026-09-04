using UMS.Modules.Organization.Domain.Common;

namespace UMS.Modules.Organization.Application.Abstractions;

/// <summary>Mirrors <c>UMS.Modules.Identity.Application.Abstractions.IDomainEventRecorder</c> exactly - see that interface's own remarks.</summary>
public interface IDomainEventRecorder
{
    public void Enqueue(IDomainEvent domainEvent);
}
