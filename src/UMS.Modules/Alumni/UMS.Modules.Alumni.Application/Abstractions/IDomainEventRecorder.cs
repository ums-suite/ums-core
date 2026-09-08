using UMS.Modules.Alumni.Domain.Common;

namespace UMS.Modules.Alumni.Application.Abstractions;

public interface IDomainEventRecorder
{
    public void Enqueue(IDomainEvent domainEvent);
}
