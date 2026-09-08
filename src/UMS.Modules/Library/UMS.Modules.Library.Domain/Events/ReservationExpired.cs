using UMS.Modules.Library.Domain.Common;

namespace UMS.Modules.Library.Domain.Events;

/// <summary>LIB-9: requirement-spec.md §8 "a Reservation expires unclaimed → the freed copy is offered to the next queued Reservation automatically." Consumers: Notifications, Reporting.</summary>
public sealed record ReservationExpired(Guid ReservationId, Guid BookId, Guid BookCopyId, Guid BorrowerId, DateTimeOffset OccurredAt) : IDomainEvent;
