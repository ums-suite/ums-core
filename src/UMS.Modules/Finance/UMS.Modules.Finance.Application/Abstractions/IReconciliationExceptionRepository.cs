using UMS.Modules.Finance.Domain.Reconciliation;

namespace UMS.Modules.Finance.Application.Abstractions;

/// <summary>FIN-14: requirement-spec.md finance §3's module-local ReconciliationException term - append-only, mirrors <see cref="ILedgerEntryRepository"/>'s own write-only-plus-read shape.</summary>
public interface IReconciliationExceptionRepository
{
    public void Add(ReconciliationException exception);
}
