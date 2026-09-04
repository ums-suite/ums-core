namespace UMS.Modules.Audit.Application.Abstractions;

/// <summary>
/// Uploads a generated export file to object storage (ADR-0010: blobs live in object storage, only
/// metadata/a reference key in Postgres - requirement-spec.md audit §8's "object-storage-backed
/// content... recorded by its metadata/reference id, never inlined" applies equally to Audit's own
/// export artifacts). Audit-module-local for now, exactly as <c>UMS.Shared.Outbox</c> stayed
/// Identity-local until a second module needed it (module-boundaries.md) - Documents (Flow #9) is
/// the next module expected to need object storage, at which point this is the natural candidate
/// to promote to a shared library.
/// </summary>
public interface IObjectStorage
{
    public Task<string> UploadAsync(string objectKey, Stream content, string contentType, CancellationToken cancellationToken = default);

    public Task<string> GetDownloadUrlAsync(string objectKey, TimeSpan expiry, CancellationToken cancellationToken = default);
}
