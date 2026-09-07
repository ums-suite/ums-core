using UMS.Modules.Finance.Domain.Common;

namespace UMS.Modules.Finance.Application.Abstractions;

/// <summary>
/// Lets an application service enqueue a domain event directly, for a write that doesn't originate
/// from a tracked aggregate's own <c>Raise</c>. Mirrors every other module's own
/// <c>IDomainEventRecorder</c> exactly.
///
/// <para>
/// FIN-12's real use: <c>LedgerEntryPosted</c> - design-decisions.md's "Ledger-Entry Atomicity
/// Mechanism" is explicit that a <c>LedgerEntry</c> row is never itself an <c>AggregateRoot</c> (it
/// raises no events of its own), so the Application service that writes the row also enqueues this
/// event directly through this interface rather than through an aggregate's <c>Raise</c>.
/// </para>
/// </summary>
public interface IDomainEventRecorder
{
    public void Enqueue(IDomainEvent domainEvent);
}
