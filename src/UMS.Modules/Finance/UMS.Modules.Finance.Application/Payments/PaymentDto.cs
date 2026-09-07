namespace UMS.Modules.Finance.Application.Payments;

public sealed record PaymentDto(
    Guid Id,
    Guid InvoiceId,
    Guid OwnerId,
    decimal Amount,
    string Currency,
    string Status,
    string GatewayName,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record InitiatePaymentRequest(Guid InvoiceId, string IdempotencyKey);

/// <param name="RedirectUrl"><c>null</c> when this call replayed an already-Pending/terminal Payment's stored state rather than making a fresh gateway call.</param>
public sealed record InitiatePaymentResult(PaymentDto Payment, string? RedirectUrl);
