namespace UMS.Modules.Finance.Application.Invoices;

public sealed record InvoiceDto(
    Guid Id,
    string SourceModule,
    string SourceReferenceId,
    string FeeType,
    Guid OwnerId,
    decimal TotalAmount,
    string Currency,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PaidAt);

public sealed record CreateInvoiceRequest(
    string SourceModule,
    string SourceReferenceId,
    string FeeType,
    Guid OwnerId,
    Guid? ApplicabilityReferenceId,
    Guid? RequestedByUserId,
    string CorrelationId);
