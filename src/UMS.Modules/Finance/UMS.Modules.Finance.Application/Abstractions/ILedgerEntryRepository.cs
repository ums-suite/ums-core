using UMS.Modules.Finance.Domain.Ledger;

namespace UMS.Modules.Finance.Application.Abstractions;

/// <summary>design-decisions.md "Ledger-Entry Atomicity Mechanism": <see cref="Add"/> only - there is deliberately no Update/Delete method anywhere on this interface (requirement-spec.md §4's append-only invariant).</summary>
public interface ILedgerEntryRepository
{
    public void Add(LedgerEntry entry);
}
