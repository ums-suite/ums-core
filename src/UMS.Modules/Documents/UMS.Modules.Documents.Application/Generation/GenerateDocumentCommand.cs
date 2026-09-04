using UMS.Modules.Documents.Domain.Common;

namespace UMS.Modules.Documents.Application.Generation;

/// <summary>
/// DOC-3: the synchronous single-document generation request (requirement-spec.md documents §2/§6).
/// Structurally single-item - there is no "documents: []" list anywhere on this type, which is
/// exactly what makes ADR-0010's "bulk generation must go through the bulk endpoint instead" rule
/// a shape-level guarantee rather than a runtime check this command's own validation would need to
/// perform (requirement-spec.md documents §4: "a request for more than one document in a single
/// call is rejected on the synchronous endpoint").
/// </summary>
/// <param name="Fields">Caller-supplied field name -&gt; display value pairs to render into the template - Documents never authors content itself (§1 Scope).</param>
/// <param name="Language">The caller's preferred language; <c>null</c> resolves to English (ADR-0011).</param>
public sealed record GenerateDocumentCommand(
    Guid OwnerId,
    DocumentType DocumentType,
    Guid SourceReferenceId,
    IReadOnlyDictionary<string, string> Fields,
    LanguageCode? Language,
    Guid RequestedByUserId,
    string CorrelationId);
