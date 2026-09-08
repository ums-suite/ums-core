using UMS.Modules.Library.Domain.Common;

namespace UMS.Modules.Library.Domain.Events;

/// <summary>
/// LIB-9: requirement-spec.md §2 - "the next queued Reservation is notified and given a bounded
/// claim window before expiring to the next in line" - this IS that notify/offer moment (not the
/// later "claimed as a Loan" moment, which needs no event of its own since <see cref="LoanIssued"/>
/// already reports it). Consumers: Notifications, Reporting. Raised via <c>IDomainEventRecorder</c>
/// since the offer itself is written by a single atomic conditional SQL UPDATE that bypasses the
/// aggregate's own change-tracked <c>Raise</c> (design-decisions.md "Reservation-Queue Fairness and
/// Claim Mechanism").
/// </summary>
public sealed record ReservationFulfilled(Guid ReservationId, Guid BookId, Guid BookCopyId, Guid BorrowerId, DateTimeOffset ClaimWindowExpiresAt, DateTimeOffset OccurredAt) : IDomainEvent;
