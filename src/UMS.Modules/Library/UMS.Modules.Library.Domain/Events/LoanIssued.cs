using UMS.Modules.Library.Domain.Common;

namespace UMS.Modules.Library.Domain.Events;

/// <summary>LIB-5: requirement-spec.md §3 - consumers: Reporting, Audit.</summary>
public sealed record LoanIssued(Guid LoanId, Guid BookCopyId, Guid BookId, Guid BorrowerId, BorrowerType BorrowerType, DateTimeOffset DueDate, DateTimeOffset OccurredAt) : IDomainEvent;
