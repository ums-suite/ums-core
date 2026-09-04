using UMS.Modules.Student.Domain.Common;

namespace UMS.Modules.Student.Application.Abstractions;

public interface IDomainEventRecorder
{
    public void Enqueue(IDomainEvent domainEvent);
}
