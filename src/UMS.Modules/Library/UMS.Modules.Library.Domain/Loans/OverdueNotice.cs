namespace UMS.Modules.Library.Domain.Loans;

/// <summary>
/// LIB-10: the idempotency marker behind <c>LoanOverdueDetectionService</c>'s own daily sweep - a
/// unique (loan_id, notice_date) row per day a Loan is found overdue, mirroring
/// <see cref="Fines.FineAccrual"/>'s own (loan_id, accrual_date) backstop applied to detection
/// instead of money: the sweep locks the Loan row, re-checks <c>Status == Active</c> post-lock (so a
/// Loan returned a moment earlier is never notified as overdue), and only raises
/// <see cref="Events.LoanOverdue"/> the first time per calendar day this marker doesn't yet exist.
/// </summary>
public sealed class OverdueNotice
{
    public OverdueNotice(Guid id, Guid loanId, DateOnly noticeDate, DateTimeOffset createdAt)
    {
        Id = id;
        LoanId = loanId;
        NoticeDate = noticeDate;
        CreatedAt = createdAt;
    }

    private OverdueNotice()
    {
    }

    public Guid Id { get; private set; }

    public Guid LoanId { get; private set; }

    public DateOnly NoticeDate { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
}
