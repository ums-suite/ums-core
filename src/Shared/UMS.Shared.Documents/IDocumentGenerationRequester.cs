using UMS.Shared.ErrorHandling.Results;

namespace UMS.Shared.Documents;

/// <summary>
/// STU-3: lets another module request a <c>GeneratedDocument</c> as a side effect of its own
/// workflow (student requirement-spec.md §2: "a Documents request for the student ID card
/// (ADR-0010)"). Mirrors <c>UMS.Shared.Notifications.INotificationRequestIntake</c>'s exact
/// pattern - living in <c>UMS.Shared.Documents</c>, not <c>UMS.Modules.Documents.*</c>, is what
/// lets a calling module (Student is the first real cross-module caller) request generation
/// without a forbidden dependency on Documents' Domain/Application/Infrastructure internals
/// (module-boundaries.md, ADR-0002). <see cref="DocumentType"/> is a caller-supplied string, not a
/// shared enum, for the same reason <c>SubmitNotificationRequestCommand.EventType</c> is a string -
/// Documents' own <c>DocumentType</c> enum stays private to its own Domain layer, this contract
/// only reuses its serialized names (<c>"IdCard"</c>, <c>"Transcript"</c>, etc.).
///
/// <para>
/// ADR-0010: "single-document generation may complete synchronously within a reasonable latency
/// budget" - this call is synchronous, in-process (ADR-0003 command semantics: one caller, one
/// authoritative result), and Documents' own generation pipeline already degrades gracefully to a
/// <c>Pending</c> result (rather than throwing) on a transient object-storage outage, so a calling
/// module can safely treat this as a fast, close-to-always-succeeding call and still choose,
/// independently, whether a non-<c>Ready</c> outcome should block its own primary operation.
/// </para>
/// </summary>
public interface IDocumentGenerationRequester
{
    public Task<Result<GeneratedDocumentSummary>> RequestAsync(RequestDocumentGenerationCommand command, CancellationToken cancellationToken = default);
}

/// <summary>One caller's request to generate a document, exactly as it calls <see cref="IDocumentGenerationRequester.RequestAsync"/>.</summary>
/// <param name="OwnerId">The person the document is about/for - Documents' own natural-key idempotency is scoped to <c>(OwnerId, DocumentType, SourceReferenceId)</c>.</param>
/// <param name="DocumentType">One of Documents' own <c>DocumentType</c> enum names, e.g. <c>"IdCard"</c>.</param>
/// <param name="SourceReferenceId">The calling module's own entity id this document concerns (e.g. the Student id) - part of Documents' idempotency natural key, so a retried request never double-generates.</param>
/// <param name="LanguageCode">One of Documents' own <c>LanguageCode</c> enum names (<c>"En"</c>/<c>"Bn"</c>); <c>null</c> resolves to English (ADR-0011).</param>
public sealed record RequestDocumentGenerationCommand(
    Guid OwnerId,
    string DocumentType,
    Guid SourceReferenceId,
    IReadOnlyDictionary<string, string> Fields,
    string? LanguageCode,
    Guid? RequestedByUserId,
    string CorrelationId);

public sealed record GeneratedDocumentSummary(Guid DocumentId, string Status, string? DownloadUrl);
