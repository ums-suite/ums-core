using UMS.Modules.Career.Domain.Common;

namespace UMS.Modules.Career.Application.Abstractions;

/// <summary>
/// Lets an application service enqueue a domain event directly for a mutation that bypasses the
/// normal <c>AggregateRoot.Raise()</c>/EF-change-tracking sweep - namely the raw-SQL guarded-insert
/// (CAR-6/CAR-11) and atomic conditional-UPDATE (CAR-12/CAR-13) write paths, which never produce a
/// tracked entity for <c>CareerDbContext</c>'s own <c>ChangeTracker.Entries&lt;IHasDomainEvents&gt;()</c>
/// sweep to find.
/// </summary>
public interface IDomainEventRecorder
{
    public void Enqueue(IDomainEvent domainEvent);
}
