using System.Security.Cryptography;
using UMS.Modules.Documents.Application.Abstractions;

namespace UMS.Modules.Documents.UnitTests.TestDoubles;

/// <summary>Computes a real MD5 over the uploaded content for its "ETag" - <see cref="Application.Generation.GeneratedDocumentPipeline"/>'s checksum-verification step compares against exactly this, mirroring real S3/MinIO's own single-part-PutObject ETag behavior.</summary>
public sealed class FakeObjectStorage : IObjectStorage
{
#pragma warning disable CA5351 // Not a security use - see this type's own remarks and GeneratedDocumentPipeline's identical suppression.
    public async Task<string> UploadAsync(string objectKey, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        using var memoryStream = new MemoryStream();
        await content.CopyToAsync(memoryStream, cancellationToken).ConfigureAwait(false);
        var md5 = Convert.ToHexStringLower(MD5.HashData(memoryStream.ToArray()));
        return $"\"{md5}\"";
    }
#pragma warning restore CA5351

    public Task<string> GetDownloadUrlAsync(string objectKey, TimeSpan expiry, CancellationToken cancellationToken = default) =>
        Task.FromResult($"https://fake-storage.test/{objectKey}");

    public Task<string> GetUploadUrlAsync(string objectKey, string contentType, TimeSpan expiry, CancellationToken cancellationToken = default) =>
        Task.FromResult($"https://fake-storage.test/upload/{objectKey}");

    public Task<ObjectMetadata?> TryGetMetadataAsync(string objectKey, CancellationToken cancellationToken = default) =>
        Task.FromResult<ObjectMetadata?>(new ObjectMetadata(1024, "\"fake-etag\""));

    public Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
