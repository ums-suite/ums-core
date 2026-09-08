using UMS.Modules.Library.Domain.Common;

namespace UMS.Modules.Library.Domain.Events;

/// <summary>LIB-11: requirement-spec.md §3 - fires incrementally, once per daily accrual increment (not only at settlement) - consumers: Notifications, Audit (Money-criticality), Reporting.</summary>
public sealed record FineAccrued(Guid FineId, Guid LoanId, Guid BorrowerId, decimal IncrementAmount, decimal TotalAmount, string Currency, DateTimeOffset OccurredAt) : IDomainEvent;
