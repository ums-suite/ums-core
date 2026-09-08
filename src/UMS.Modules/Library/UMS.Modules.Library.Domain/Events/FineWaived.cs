using UMS.Modules.Library.Domain.Common;

namespace UMS.Modules.Library.Domain.Events;

/// <summary>LIB-14: requirement-spec.md §4 "A Fine waiver requires an audited actor + reason (Money-criticality tier)." Consumers: Notifications, Audit, Reporting.</summary>
public sealed record FineWaived(Guid FineId, Guid LoanId, Guid BorrowerId, Guid WaivedByUserId, string Reason, DateTimeOffset OccurredAt) : IDomainEvent;
