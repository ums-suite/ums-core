using UMS.Modules.Library.Domain.Common;

namespace UMS.Modules.Library.Domain.Events;

/// <summary>LIB-8: requirement-spec.md §3 - consumers: Reporting, and (§2 Return) triggers the reservation-queue-fulfillment relay (LIB-9).</summary>
public sealed record LoanReturned(Guid LoanId, Guid BookCopyId, Guid BookId, Guid BorrowerId, bool WasOverdue, DateTimeOffset OccurredAt) : IDomainEvent;
