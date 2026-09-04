namespace UMS.Modules.Documents.Application.Abstractions;

/// <summary>
/// ADR-0010: artifact bytes live in S3-compatible object storage, only the key/metadata lives in
/// Postgres. Module-local, mirroring Audit's own local <c>IObjectStorage</c> exactly (per the
/// build brief: each module owns its own copy of this pattern today, since it has not yet been
/// promoted to a shared library - Audit's own doc comment already names Documents as "the next
/// module expected to need object storage... the natural candidate to promote"). Extended here
/// with <see cref="DeleteAsync"/>, which Audit's export-only use case never needed: Documents'
/// saga-style upload-then-commit flow (design-decisions.md) requires a compensating cleanup path
/// for an orphaned object whose metadata row never reached <c>Ready</c>.
/// </summary>
public interface IObjectStorage
{
    /// <summary>Uploads the artifact and returns the object's ETag (used by the caller to verify the upload's checksum without a separate round trip - design-decisions.md's saga-style flow).</summary>
    public Task<string> UploadAsync(string objectKey, Stream content, string contentType, CancellationToken cancellationToken = default);

    public Task<string> GetDownloadUrlAsync(string objectKey, TimeSpan expiry, CancellationToken cancellationToken = default);

    /// <summary>requirement-spec.md documents §2 Uploaded Artifact Storage: "caller requests a presigned upload URL ... client uploads directly to object storage using that URL." A pre-signed <c>PUT</c>, never a Documents-proxied upload stream.</summary>
    public Task<string> GetUploadUrlAsync(string objectKey, string contentType, TimeSpan expiry, CancellationToken cancellationToken = default);

    /// <summary>Confirms the object exists and returns its size/ETag for checksum comparison - the upload-confirm step's existence/checksum verification (§2).</summary>
    public Task<ObjectMetadata?> TryGetMetadataAsync(string objectKey, CancellationToken cancellationToken = default);

    /// <summary>Compensating-cleanup primitive (edge-cases.md's object-storage/DB-ordering decision) - deletes an orphaned object whose metadata row never reached <c>Ready</c>. Idempotent: deleting an already-absent key is not an error.</summary>
    public Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default);
}
