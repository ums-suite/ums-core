using UMS.Modules.Library.Domain.Common;

namespace UMS.Modules.Library.Domain.Events;

/// <summary>LIB-13: requirement-spec.md §3 - raised once Finance's PaymentCompleted (PaymentSucceeded) is applied. Consumers: Notifications, Audit, Reporting.</summary>
public sealed record FineSettled(Guid FineId, Guid LoanId, Guid BorrowerId, decimal Amount, string Currency, DateTimeOffset OccurredAt) : IDomainEvent;
