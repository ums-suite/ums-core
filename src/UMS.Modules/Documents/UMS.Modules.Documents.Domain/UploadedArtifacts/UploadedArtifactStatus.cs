namespace UMS.Modules.Documents.Domain.UploadedArtifacts;

/// <summary>requirement-spec.md documents §2: "A row stuck below Ready ... is never treated as a valid reference by any calling module."</summary>
public enum UploadedArtifactStatus
{
    /// <summary>A presigned upload URL was issued; the client's direct-to-storage upload has not yet been confirmed.</summary>
    PendingUpload = 0,

    Ready = 1,

    /// <summary>Confirmed, but existence/checksum verification failed.</summary>
    Failed = 2,
}
