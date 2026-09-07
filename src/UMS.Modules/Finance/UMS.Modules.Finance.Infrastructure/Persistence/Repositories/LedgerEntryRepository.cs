using UMS.Modules.Finance.Application.Abstractions;
using UMS.Modules.Finance.Domain.Ledger;

namespace UMS.Modules.Finance.Infrastructure.Persistence.Repositories;

internal sealed class LedgerEntryRepository(FinanceDbContext context) : ILedgerEntryRepository
{
    public void Add(LedgerEntry entry) => context.LedgerEntries.Add(entry);
}
