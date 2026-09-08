using UMS.Modules.Library.Domain.Common;

namespace UMS.Modules.Library.Domain.Events;

/// <summary>LIB-7: requirement-spec.md §3 - consumers: Reporting.</summary>
public sealed record LoanRenewed(Guid LoanId, Guid BorrowerId, DateTimeOffset NewDueDate, int RenewalCount, DateTimeOffset OccurredAt) : IDomainEvent;
