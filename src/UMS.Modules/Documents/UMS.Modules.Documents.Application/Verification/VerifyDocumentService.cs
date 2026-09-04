using UMS.Modules.Documents.Application.Abstractions;
using UMS.Modules.Documents.Domain.GeneratedDocuments;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Documents.Application.Verification;

/// <summary>
/// DOC-9: the public, unauthenticated verify lookup (requirement-spec.md documents §2/§4/§6/§8).
/// Never returns the raw artifact bytes or a download URL - only validity/type/owner-name/issue-date/
/// status - and never a bare 404 for a document that once existed: a Revoked or Superseded document
/// resolves with its true current status and reason, so a forged claim that a canceled certificate
/// is still valid is visibly refutable. A verification id that never existed at all (a garbage
/// value, not one of a real, once-issued document) is the one case this DOES return 404 for -
/// distinct from the "once existed" case §4/§8 are actually protecting.
/// </summary>
public sealed class VerifyDocumentService(IGeneratedDocumentRepository documents)
{
    public async Task<Result<DocumentVerificationDto>> VerifyAsync(string verificationId, CancellationToken cancellationToken = default)
    {
        var document = await documents.GetByVerificationIdAsync(verificationId, cancellationToken).ConfigureAwait(false);
        if (document is null)
        {
            return Error.NotFound("document.verification_id_not_found", $"No document was ever issued with verification id '{verificationId}'.");
        }

        var (isValid, reason) = document.Status switch
        {
            GeneratedDocumentStatus.Ready => (true, (string?)null),
            GeneratedDocumentStatus.Revoked => (false, $"This document was revoked{(document.RevokedReason is null ? "." : $": {document.RevokedReason}")}"),
            GeneratedDocumentStatus.Superseded => (false, "This document has been superseded by a corrected re-issue and is no longer valid."),
            _ => (false, "This document has not finished being generated."),
        };

        return new DocumentVerificationDto(
            document.DigitalVerificationId,
            document.DocumentType.ToString(),
            document.Status.ToString(),
            isValid,
            reason,
            document.CreatedAt,
            document.ReadyAt);
    }
}
