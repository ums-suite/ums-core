using UMS.Modules.Library.Domain.Common;

namespace UMS.Modules.Library.Domain.Events;

/// <summary>LIB-17: requirement-spec.md §8 "a BookCopy is reported lost mid-loan → the Loan is force-closed" - not literally named in §3's event catalog, but the same "&lt;Entity&gt;&lt;PastTenseVerb&gt;" naming convention extended to a real, audited transition (consumers: Reporting, Audit).</summary>
public sealed record LoanLostWriteOff(Guid LoanId, Guid BookCopyId, Guid BookId, Guid BorrowerId, DateTimeOffset OccurredAt) : IDomainEvent;
