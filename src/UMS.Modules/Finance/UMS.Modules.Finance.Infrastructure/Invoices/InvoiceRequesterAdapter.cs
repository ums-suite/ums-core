using UMS.Modules.Finance.Application.Invoices;
using UMS.Shared.ErrorHandling.Results;
using UMS.Shared.Finance;

namespace UMS.Modules.Finance.Infrastructure.Invoices;

/// <summary>
/// FIN-2: the real implementation of <see cref="IInvoiceRequester"/>, registered here (Finance's own
/// composition root) so a future in-process caller (Admission, first at release/DEVELOPMENT_PLAN.md
/// Flow #15) resolves it directly - no HTTP hop, the "application-service interface" ADR-0002/
/// ADR-0008 describe as the real cross-module mechanism in this modular monolith. Delegates straight
/// to <see cref="InvoiceService"/>, the identical application service the HTTP
/// <c>POST /api/v1/finance/invoices</c> endpoint also calls.
/// </summary>
internal sealed class InvoiceRequesterAdapter(InvoiceService invoiceService) : IInvoiceRequester
{
    public async Task<Result<InvoiceSummary>> CreateInvoiceAsync(CreateInvoiceCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var result = await invoiceService.CreateAsync(
            new CreateInvoiceRequest(command.SourceModule, command.SourceReferenceId, command.FeeType, command.OwnerId, command.ApplicabilityReferenceId, command.RequestedByUserId, command.CorrelationId),
            cancellationToken).ConfigureAwait(false);

        return result.Match<Result<InvoiceSummary>>(
            dto => new InvoiceSummary(dto.Id, dto.Status, dto.TotalAmount, dto.Currency),
            error => error);
    }
}
