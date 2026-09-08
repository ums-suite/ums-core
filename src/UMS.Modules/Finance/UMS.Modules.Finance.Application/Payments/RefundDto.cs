namespace UMS.Modules.Finance.Application.Payments;

public sealed record RefundDto(
    Guid Id,
    Guid PaymentId,
    decimal Amount,
    string Currency,
    string Status,
    string Method,
    string? GatewayRefundReference,
    DateTimeOffset CreatedAt);

/// <param name="Reason">Optional operator-supplied reason, carried through to the Audit entry (ADR-0012) - not part of the domain invariant itself.</param>
public sealed record RefundPaymentRequest(decimal Amount, string? Reason);
