namespace UMS.Modules.Documents.Domain.Common;

/// <summary>
/// requirement-spec.md documents §4's last bullet: "Document generation, revocation, and bulk-job
/// completion are recorded to Audit synchronously for any document type backing an official
/// academic or financial record (transcript, certificate, admit card, receipt) - ID cards follow
/// the same rule since they are identity-bearing." Every one of the six v1 types except
/// <see cref="DocumentType.MeritList"/> is official-record - kept as an explicit predicate here
/// (not "== MeritList" inverted at every call site) so a future seventh type must make a
/// conscious decision rather than silently defaulting one way or the other.
/// </summary>
public static class DocumentTypeExtensions
{
    public static bool IsOfficialRecord(this DocumentType type) => type switch
    {
        DocumentType.Transcript => true,
        DocumentType.Certificate => true,
        DocumentType.AdmitCard => true,
        DocumentType.Receipt => true,
        DocumentType.IdCard => true,
        DocumentType.MeritList => false,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unrecognized DocumentType."),
    };
}
