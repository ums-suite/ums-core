namespace UMS.Modules.Library.Application.Fines;

public sealed record FineDto(
    Guid Id,
    Guid LoanId,
    Guid BorrowerId,
    string BorrowerType,
    string Reason,
    decimal Amount,
    string Currency,
    string Status,
    Guid? InvoiceId,
    Guid? WaivedByUserId,
    string? WaivedReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset? SettledAt,
    DateTimeOffset? WaivedAt);

public sealed record WaiveFineRequest(string Reason);
