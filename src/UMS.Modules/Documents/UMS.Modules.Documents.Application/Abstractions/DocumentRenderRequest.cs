using UMS.Modules.Documents.Domain.Common;
using UMS.Modules.Documents.Domain.Templates;

namespace UMS.Modules.Documents.Application.Abstractions;

/// <summary>Everything <see cref="IDocumentRenderer"/> needs to render one PDF - see that interface's own remarks.</summary>
/// <param name="Template">The pinned template version to render against (never re-resolved to "current" mid-render).</param>
/// <param name="Language">Resolved server-side (ADR-0011) - the caller's preferred language, English fallback already applied by the time this reaches the renderer.</param>
/// <param name="Fields">Caller-supplied field name -&gt; display value pairs (e.g. "studentName" -&gt; "Rahim Uddin").</param>
/// <param name="DigitalVerificationId">Embedded in the in-PDF QR code as the public verify URL's path segment.</param>
/// <param name="IssuedAt">Printed on the document as its issue date.</param>
public sealed record DocumentRenderRequest(
    DocumentTemplate Template,
    LanguageCode Language,
    IReadOnlyDictionary<string, string> Fields,
    string DigitalVerificationId,
    DateTimeOffset IssuedAt);
