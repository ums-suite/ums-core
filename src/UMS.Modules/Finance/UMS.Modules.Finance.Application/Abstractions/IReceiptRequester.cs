using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Finance.Application.Abstractions;

/// <summary>
/// FIN-15: this module's own port over Documents' shared
/// <c>UMS.Shared.Documents.IDocumentGenerationRequester</c> contract - the same module-local-port
/// shape Learning's own <c>IUploadedArtifactGateway</c> uses over Documents' other shared contract,
/// so the Application layer depends on a Finance-owned abstraction while the Infrastructure adapter
/// does the cross-module translation.
///
/// <para>
/// Called synchronously, right after a Payment's success transaction commits (ADR-0010's explicit
/// "receipt right after payment" example of allowed synchronous generation; requirement-spec.md §7)
/// - best-effort, the same posture Student's own STU-3 Documents call established: a Documents
/// outage never rolls back or fails the Payment that already committed.
/// </para>
/// </summary>
public interface IReceiptRequester
{
    public Task<Result<Guid>> RequestReceiptAsync(RequestReceiptCommand command, CancellationToken cancellationToken = default);
}

public sealed record RequestReceiptCommand(Guid OwnerId, Guid PaymentId, decimal Amount, string Currency, DateTimeOffset PaidAt);
