namespace UMS.Modules.Hostel.Application.Allocations;

public sealed record AllocationDto(
    Guid Id,
    Guid StudentId,
    Guid BedId,
    Guid RoomId,
    Guid HostelId,
    Guid HostelApplicationId,
    string Status,
    Guid? InvoiceId,
    DateTimeOffset FeeGraceDeadline,
    string? CheckOutKind,
    bool RefundRequested,
    DateTimeOffset CreatedAt,
    DateTimeOffset? FeePaidAt,
    DateTimeOffset? ActivatedAt,
    DateTimeOffset? CheckedOutAt,
    DateTimeOffset? ExpiredAt);

public sealed record CheckOutRequest(string CheckOutType);
