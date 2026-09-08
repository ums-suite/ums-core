namespace UMS.Modules.Library.Application.Reservations;

public sealed record ReservationDto(
    Guid Id,
    Guid BookId,
    Guid BorrowerId,
    string BorrowerType,
    int Priority,
    string Status,
    Guid? OfferedCopyId,
    DateTimeOffset? OfferedAt,
    DateTimeOffset? ClaimWindowExpiresAt,
    DateTimeOffset? ClaimedAt,
    DateTimeOffset? ExpiredAt,
    DateTimeOffset CreatedAt);
