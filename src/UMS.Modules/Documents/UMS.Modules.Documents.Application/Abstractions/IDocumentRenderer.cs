using UMS.Modules.Documents.Domain.Common;
using UMS.Modules.Documents.Domain.Templates;

namespace UMS.Modules.Documents.Application.Abstractions;

/// <summary>
/// DOC-2/DOC-8: renders one <see cref="DocumentType"/> from a pinned <see cref="DocumentTemplate"/>
/// version plus caller-supplied field data into PDF bytes, embedding a QR code that encodes the
/// public verify URL for the request's <c>DigitalVerificationId</c> (requirement-spec.md documents
/// §2 QR / Digital Verification). Documents never authors document <em>content</em> (§1 Scope) -
/// the request's <c>Fields</c> is exactly the data the calling module supplied; this renderer only
/// lays it out.
/// </summary>
public interface IDocumentRenderer
{
    public byte[] Render(DocumentRenderRequest request);
}
