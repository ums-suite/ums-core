using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using UMS.Modules.Documents.Application.Abstractions;

namespace UMS.Modules.Documents.Infrastructure.Storage;

/// <summary>
/// DOC-2/ADR-0010: blobs live in object storage, only the key/metadata lives in Postgres. Talks to
/// MinIO (local dev/self-hosted, S3-compatible) or real AWS S3 through the same client, since MinIO
/// deliberately implements the S3 API (ums-devops's docker-compose already runs a MinIO container
/// for exactly this purpose) - mirrors Audit's own <c>S3ObjectStorage</c>.
///
/// <para>
/// Unlike Audit's own copy, every call here is wrapped in a Polly resilience pipeline
/// (ums-conventions.md, Resilience &amp; Reliability: "every call to ... object storage is wrapped
/// in Polly"). <c>AWSSDK.S3</c> does not go through <c>HttpClientFactory</c>, so
/// <c>UMS.Shared.Resilience.Http.AddUmsResilientHttpClient</c> (which wraps
/// <c>AddStandardResilienceHandler</c> onto a named/typed <see cref="HttpClient"/>) does not apply
/// here - this class builds its own <c>Polly.Core</c> pipeline directly with the same
/// shape (retry with exponential backoff + jitter, a circuit breaker, a timeout) instead.
/// </para>
/// </summary>
internal sealed class S3ObjectStorage : IObjectStorage, IDisposable
{
    private readonly AmazonS3Client _client;
    private readonly string _bucket;
    private readonly Protocol _presignedUrlProtocol;
    private readonly ResiliencePipeline _resilience;

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

        _resilience = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                Delay = TimeSpan.FromMilliseconds(200),
                ShouldHandle = new PredicateBuilder().Handle<AmazonS3Exception>().Handle<TimeoutException>(),
            })
            .AddCircuitBreaker(new Polly.CircuitBreaker.CircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5,
                MinimumThroughput = 5,
                SamplingDuration = TimeSpan.FromSeconds(30),
                BreakDuration = TimeSpan.FromSeconds(15),
                ShouldHandle = new PredicateBuilder().Handle<AmazonS3Exception>(),
            })
            .AddTimeout(TimeSpan.FromSeconds(10))
            .Build();
    }

    public async Task<string> UploadAsync(string objectKey, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        var response = await _resilience.ExecuteAsync(
            async ct =>
            {
                // Every Polly retry attempt re-enters this lambda against the SAME caller-owned
                // stream - PutObjectRequest.AutoCloseStream defaults to true, which disposes it
                // after the first attempt and made a genuine retry throw ObjectDisposedException
                // (caught during this build's own manual Host run-through, not a hypothetical).
                // AutoCloseStream=false plus rewinding a seekable stream before each attempt is
                // what actually makes the retry-with-the-same-stream pattern safe.
                if (content.CanSeek)
                {
                    content.Position = 0;
                }

                var request = new PutObjectRequest
                {
                    BucketName = _bucket,
                    Key = objectKey,
                    InputStream = content,
                    ContentType = contentType,
                    AutoCloseStream = false,
                };

                return await _client.PutObjectAsync(request, ct).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);

        return response.ETag;
    }

    public Task<string> GetDownloadUrlAsync(string objectKey, TimeSpan expiry, CancellationToken cancellationToken = default)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _bucket,
            Key = objectKey,
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.Add(expiry),

            // GetPreSignedUrlRequest.Protocol is independent of AmazonS3Config.UseHttp - it
            // defaults to HTTPS regardless, which breaks against a plain-HTTP MinIO endpoint
            // (local dev/test) unless set explicitly here too.
            Protocol = _presignedUrlProtocol,
        };

        return Task.FromResult(_client.GetPreSignedURL(request));
    }

    public Task<string> GetUploadUrlAsync(string objectKey, string contentType, TimeSpan expiry, CancellationToken cancellationToken = default)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _bucket,
            Key = objectKey,
            Verb = HttpVerb.PUT,
            ContentType = contentType,
            Expires = DateTime.UtcNow.Add(expiry),
            Protocol = _presignedUrlProtocol,
        };

        return Task.FromResult(_client.GetPreSignedURL(request));
    }

    public async Task<ObjectMetadata?> TryGetMetadataAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _resilience.ExecuteAsync(
                async ct => await _client.GetObjectMetadataAsync(new GetObjectMetadataRequest { BucketName = _bucket, Key = objectKey }, ct).ConfigureAwait(false),
                cancellationToken).ConfigureAwait(false);

            return new ObjectMetadata(response.ContentLength, response.ETag);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        await _resilience.ExecuteAsync(
            async ct => await _client.DeleteObjectAsync(new DeleteObjectRequest { BucketName = _bucket, Key = objectKey }, ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Idempotent bucket provisioning, called once from <c>UseDocumentsModuleAsync</c> at Host/
    /// Workers startup (mirrors this build's own integration-test fixture, which had to do the
    /// same against its disposable MinIO container). A real deployment provisions the bucket via
    /// infra tooling ahead of time, same as Audit's own <c>ums-documents</c> bucket - this call is
    /// the safety net for local/dev environments where that step is easy to forget, confirmed
    /// necessary during this build's own manual Host run-through: the bucket did not already
    /// exist against the shared dev MinIO instance.
    /// </summary>
    public async Task EnsureBucketExistsAsync(CancellationToken cancellationToken = default)
    {
        var exists = await Amazon.S3.Util.AmazonS3Util.DoesS3BucketExistV2Async(_client, _bucket).ConfigureAwait(false);
        if (!exists)
        {
            await _client.PutBucketAsync(new PutBucketRequest { BucketName = _bucket }, cancellationToken).ConfigureAwait(false);
        }
    }

    public void Dispose() => _client.Dispose();
}
