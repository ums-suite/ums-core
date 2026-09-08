using UMS.Modules.Finance.Application.Abstractions;
using UMS.Modules.Finance.Domain.Reconciliation;

namespace UMS.Modules.Finance.Infrastructure.Persistence.Repositories;

internal sealed class ReconciliationExceptionRepository(FinanceDbContext context) : IReconciliationExceptionRepository
{
    public void Add(ReconciliationException exception) => context.ReconciliationExceptions.Add(exception);
}
