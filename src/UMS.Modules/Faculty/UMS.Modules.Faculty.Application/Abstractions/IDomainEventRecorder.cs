using UMS.Modules.Faculty.Domain.Common;

namespace UMS.Modules.Faculty.Application.Abstractions;

public interface IDomainEventRecorder
{
    public void Enqueue(IDomainEvent domainEvent);
}
