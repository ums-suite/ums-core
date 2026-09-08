using UMS.Modules.Library.Domain.Common;

namespace UMS.Modules.Library.Domain.Events;

/// <summary>LIB-4: requirement-spec.md §3 - consumers: Notifications, Reporting.</summary>
public sealed record ReservationCreated(Guid ReservationId, Guid BookId, Guid BorrowerId, BorrowerType BorrowerType, DateTimeOffset OccurredAt) : IDomainEvent;
