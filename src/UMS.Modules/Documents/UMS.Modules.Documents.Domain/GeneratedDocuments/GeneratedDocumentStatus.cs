namespace UMS.Modules.Documents.Domain.GeneratedDocuments;

/// <summary>
/// requirement-spec.md documents §4/§3: "GeneratedDocument.status only becomes Ready after the
/// render and the object-storage upload and a checksum verification all succeed"; design-decisions.md's
/// claim-row and saga-style upload-then-commit decisions add the two intermediate states.
/// Legal transitions (enforced by <see cref="GeneratedDocument"/>, never by a caller):
/// <c>Pending</c> -&gt; <c>Uploaded</c> -&gt; <c>Ready</c> -&gt; (<c>Revoked</c> | <c>Superseded</c>);
/// <c>Pending</c>/<c>Uploaded</c> -&gt; <c>Failed</c> (compensating cleanup, edge-cases.md's
/// object-storage/DB-ordering decision); <c>Failed</c> -&gt; <c>Pending</c> (a retried request
/// re-claims a natural key that never reached <c>Ready</c>, requirement-spec.md documents §8's
/// duplicate-generation-request edge case).
/// </summary>
public enum GeneratedDocumentStatus
{
    /// <summary>The claim row - reserves the <c>(ownerId, documentType, sourceReferenceId)</c> natural key before rendering starts (edge-cases.md, idempotent-generation race decision).</summary>
    Pending = 0,

    /// <summary>Object-storage upload succeeded and the (unverified) object key is attached; checksum verification has not yet completed (design-decisions.md's saga-style two-phase flow).</summary>
    Uploaded = 1,

    /// <summary>Render + upload + checksum verification all succeeded (requirement-spec.md documents §4). Terminal for the "successful" path - immutable from here except for the Revoked/Superseded transitions.</summary>
    Ready = 2,

    /// <summary>Admin/Registrar-revoked (requirement-spec.md documents §4/§6) - permanently resolvable at the verify endpoint, never deleted.</summary>
    Revoked = 3,

    /// <summary>Replaced by a corrected re-issue - the new GeneratedDocument's id is recorded on this row (requirement-spec.md documents §4).</summary>
    Superseded = 4,

    /// <summary>Never reached Ready - a failed render, a failed/unverifiable upload, or the compensating cleanup sweep's own terminal outcome (edge-cases.md's object-storage/DB-ordering decision). The natural key may be re-claimed from this state.</summary>
    Failed = 5,
}
