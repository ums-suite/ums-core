using System.Globalization;
using UMS.Modules.Finance.Application.Abstractions;
using UMS.Shared.Documents;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Finance.Infrastructure.Documents;

/// <summary>
/// FIN-15: adapts this module's own <see cref="IReceiptRequester"/> port onto Documents' real,
/// shared <see cref="IDocumentGenerationRequester"/> cross-module contract - the same in-process
/// adapter pattern Learning's own <c>UploadedArtifactGatewayAdapter</c> uses in the identical
/// direction, so Finance never takes a forbidden dependency on <c>UMS.Modules.Documents.*</c>
/// internals (module-boundaries.md, ADR-0002).
/// </summary>
internal sealed class ReceiptRequesterAdapter(IDocumentGenerationRequester requester) : IReceiptRequester
{
    private const string ReceiptDocumentType = "Receipt";

    public async Task<Result<Guid>> RequestReceiptAsync(RequestReceiptCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var result = await requester.RequestAsync(
            new RequestDocumentGenerationCommand(
                command.OwnerId,
                ReceiptDocumentType,
                command.PaymentId,
                new Dictionary<string, string>
                {
                    ["amount"] = command.Amount.ToString("F2", CultureInfo.InvariantCulture),
                    ["currency"] = command.Currency,
                    ["paidAt"] = command.PaidAt.ToString("O"),
                },
                LanguageCode: null,
                RequestedByUserId: null,
                CorrelationId: command.PaymentId.ToString()),
            cancellationToken).ConfigureAwait(false);

        return result.Match<Result<Guid>>(summary => summary.DocumentId, error => error);
    }
}
