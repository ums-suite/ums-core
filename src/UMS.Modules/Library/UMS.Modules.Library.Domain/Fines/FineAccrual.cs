namespace UMS.Modules.Library.Domain.Fines;

/// <summary>
/// LIB-11: design-decisions.md "Fine-Accrual Job Idempotency" - the (loan_id, accrual_date) unique
/// backstop behind the accrual job's own row-lock-and-recheck. Modeled as its own top-level,
/// independently repository-backed entity (own table, own Guid id) rather than an
/// <c>OwnsMany</c> child of <see cref="Fine"/> - per mechanism #13's own guidance, a child keyed by a
/// caller-supplied natural key ((loan_id, accrual_date), not a fresh Guid) that gets appended to an
/// already-persisted parent well after its initial insert is exactly the shape that hits EF Core
/// 10.0.11's owned-collection insert-vs-update misfire; a standalone entity sidesteps the bug
/// entirely rather than working around it with a shadow Ordinal key.
/// </summary>
public sealed class FineAccrual
{
    public FineAccrual(Guid id, Guid fineId, Guid loanId, DateOnly accrualDate, decimal amount, DateTimeOffset createdAt)
    {
        Id = id;
        FineId = fineId;
        LoanId = loanId;
        AccrualDate = accrualDate;
        Amount = amount;
        CreatedAt = createdAt;
    }

    private FineAccrual()
    {
    }

    public Guid Id { get; private set; }

    public Guid FineId { get; private set; }

    public Guid LoanId { get; private set; }

    public DateOnly AccrualDate { get; private set; }

    public decimal Amount { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
}
