using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using UMS.Modules.Audit.Application.Abstractions;

namespace UMS.Modules.Audit.Infrastructure.Storage;

/// <summary>
/// AUD-9's export artifact storage - blobs live in object storage, only the key lives in Postgres
/// (ADR-0010). Talks to MinIO (local dev/self-hosted, S3-compatible) or real AWS S3 through the
/// same client, since MinIO deliberately implements the S3 API (ums-devops's docker-compose already
/// runs a MinIO container for exactly this purpose).
/// </summary>
internal sealed class S3ObjectStorage : IObjectStorage, IDisposable
{
    private readonly AmazonS3Client _client;
    private readonly string _bucket;
    private readonly Protocol _presignedUrlProtocol;

    public S3ObjectStorage(IOptions<ObjectStorageOptions> options)
    {
        var config = options.Value;
        _bucket = config.Bucket;
        _presignedUrlProtocol = config.UseHttps ? Protocol.HTTPS : Protocol.HTTP;

        var s3Config = new AmazonS3Config
        {
            ServiceURL = config.Endpoint,
            ForcePathStyle = true, // MinIO requires path-style addressing (bucket in the path, not a DNS subdomain).
            UseHttp = !config.UseHttps,
        };

        _client = new AmazonS3Client(config.AccessKey, config.SecretKey, s3Config);
    }

    public async Task<string> UploadAsync(string objectKey, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        var request = new PutObjectRequest
        {
            BucketName = _bucket,
            Key = objectKey,
            InputStream = content,
            ContentType = contentType,
        };

        await _client.PutObjectAsync(request, cancellationToken).ConfigureAwait(false);

        return objectKey;
    }

    public Task<string> GetDownloadUrlAsync(string objectKey, TimeSpan expiry, CancellationToken cancellationToken = default)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _bucket,
            Key = objectKey,
            Expires = DateTime.UtcNow.Add(expiry),

            // GetPreSignedUrlRequest.Protocol is independent of AmazonS3Config.UseHttp - it
            // defaults to HTTPS regardless, which breaks against a plain-HTTP MinIO endpoint
            // (local dev/test) unless set explicitly here too.
            Protocol = _presignedUrlProtocol,
        };

        return Task.FromResult(_client.GetPreSignedURL(request));
    }

    public void Dispose() => _client.Dispose();
}
