using UMS.Shared.ErrorHandling.Results;

namespace UMS.Shared.Finance;

/// <summary>
/// FIN-2: the ONLY way another module raises an Invoice against the shared payment core
/// (requirement-spec.md finance §2 Invoice Generation: "Finance never initiates an Invoice on its
/// own trigger" - Admission/Student/Hostel/Library/Alumni are the only real callers). Mirrors
/// <c>UMS.Shared.Documents.IDocumentGenerationRequester</c>'s exact pattern - living in
/// <c>UMS.Shared.Finance</c>, not <c>UMS.Modules.Finance.*</c>, is what lets a calling module
/// request an Invoice without a forbidden dependency on Finance's Domain/Application/
/// Infrastructure internals (module-boundaries.md, ADR-0002).
///
/// <para>
/// <b>release/DEVELOPMENT_PLAN.md Flow #14 "Payment Core" scope note:</b> Admission (Flow #15) is
/// this contract's first real caller, built immediately after this one. No module in this repo
/// calls it yet - exactly the same "one shared contract, no consumer built yet" posture
/// Notifications' own <c>INotificationRequestIntake</c> shipped in at Flow #8, and Documents' own
/// <c>IDocumentGenerationRequester</c> shipped in at Flow #9, before Student (Flow #11) became
/// their first real caller.
/// </para>
///
/// <para>
/// This same operation is also reachable over HTTP as
/// <c>POST /api/v1/finance/invoices</c> (requirement-spec.md finance §6, gated by
/// <c>finance.invoice.create</c>) - the literal endpoint the requirement-spec's own API-surface
/// table names, for a system credential or direct testing. A future in-process caller
/// (Admission/Student/Hostel/Library/Alumni) should prefer THIS interface: no HTTP hop, and it is
/// the "application-service interface" ADR-0002/ADR-0008 describe as the real cross-module
/// mechanism in this modular monolith.
/// </para>
/// </summary>
public interface IInvoiceRequester
{
    /// <summary>
    /// Idempotent by the <c>(SourceModule, SourceReferenceId, FeeType)</c> natural key
    /// (requirement-spec.md finance §2/§4/§8: "a caller's own retry can never silently double-bill")
    /// - a retried or genuinely concurrent call for the same tuple returns the already-committed
    /// Invoice, never a duplicate and never a raw constraint-violation error.
    /// </summary>
    public Task<Result<InvoiceSummary>> CreateInvoiceAsync(CreateInvoiceCommand command, CancellationToken cancellationToken = default);
}

/// <summary>One caller's request to raise an Invoice, exactly as it calls <see cref="IInvoiceRequester.CreateInvoiceAsync"/>.</summary>
/// <param name="SourceModule">The calling module's own lowercase name, e.g. <c>"admission"</c> (ums-conventions.md Naming).</param>
/// <param name="SourceReferenceId">The calling module's own entity id this Invoice concerns (e.g. an <c>applicationId</c>) - part of the dedupe natural key.</param>
/// <param name="FeeType">One of Finance's own <c>FeeStructure</c> fee-type names (e.g. <c>"ApplicationFee"</c>) - a caller-supplied string, not a shared enum, the same reason <c>SubmitNotificationRequestCommand.EventType</c> is a string (Finance's own <c>FeeType</c>/applicability concepts stay private to its Domain layer).</param>
/// <param name="OwnerId">The Identity <c>UserId</c> who owns/will pay this Invoice.</param>
/// <param name="ApplicabilityReferenceId">
/// Optional - a Program or AdmissionCampaign id when the caller's own fee genuinely varies by one
/// (e.g. a per-Program tuition amount). <c>null</c> resolves against a Service-scoped FeeStructure
/// named exactly <paramref name="FeeType"/> instead (this build's Payment Core slice's only real
/// caller - Admission's flat application fee - needs nothing more specific; Student's own
/// per-Program tuition fee is Phase-3 remainder-Finance work, Flow #18).
/// </param>
/// <param name="RequestedByUserId">The acting user for Audit purposes; <c>null</c> for a system-originated call.</param>
/// <param name="CorrelationId">The request/event chain's correlation id, threaded end-to-end (ums-conventions.md, Observability).</param>
public sealed record CreateInvoiceCommand(
    string SourceModule,
    string SourceReferenceId,
    string FeeType,
    Guid OwnerId,
    Guid? ApplicabilityReferenceId,
    Guid? RequestedByUserId,
    string CorrelationId);

public sealed record InvoiceSummary(Guid InvoiceId, string Status, decimal TotalAmount, string Currency);
