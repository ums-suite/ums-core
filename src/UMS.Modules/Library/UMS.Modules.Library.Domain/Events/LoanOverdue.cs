using UMS.Modules.Library.Domain.Common;

namespace UMS.Modules.Library.Domain.Events;

/// <summary>
/// LIB-10: requirement-spec.md §3 - "a scheduled/detected transition (computed, not a distinct
/// persisted status)"; consumers: Notifications, Fine-accrual job. Raised via
/// <c>IDomainEventRecorder</c> by <c>LoanOverdueDetectionService</c>'s own idempotent sweep
/// (<c>library.overdue_notices</c>'s (loan_id, notice_date) unique backstop) - never by a
/// <see cref="Loans.Loan"/> state-transition method, since Overdue is never written as a status
/// column on the Loan itself.
/// </summary>
public sealed record LoanOverdue(Guid LoanId, Guid BookCopyId, Guid BorrowerId, DateTimeOffset DueDate, DateTimeOffset OccurredAt) : IDomainEvent;
