using UMS.Modules.Identity.Domain.Common;

namespace UMS.Modules.Identity.Application.Abstractions;

/// <summary>
/// Records a domain event that did not originate from a loaded aggregate's own method - e.g.
/// <c>UserLoginFailed</c> against an identifier that resolved to no <see cref="Domain.Users.User"/>
/// at all. <see cref="IUnitOfWork.SaveChangesAsync"/>'s implementation drains both this recorder
/// and every tracked aggregate's own <c>DomainEvents</c> into the same transactional outbox write
/// (ADR-0003).
/// </summary>
public interface IDomainEventRecorder
{
    public void Enqueue(IDomainEvent domainEvent);
}
