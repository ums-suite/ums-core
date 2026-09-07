using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using Testcontainers.Minio;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace UMS.Modules.Learning.IntegrationTests.Infrastructure;

/// <summary>
/// Boots the real <c>UMS.Host</c> pipeline against real, disposable Postgres/Redis/MinIO containers
/// - mirrors <c>AcademicApiFixture</c> exactly, plus MinIO (as <c>DocumentsApiFixture</c> does)
/// because LRN-5/LRN-13's presigned-upload flow goes through Documents' real object-storage
/// integration end to end, not a stub.
///
/// <para>
/// The plagiarism-check provider is deliberately configured with <c>FailureRate = 0</c> here so the
/// default suite is deterministic; the failure/outage path gets its own explicitly-configured
/// coverage in <c>PlagiarismCheckTests</c>' own fixture-independent assertions.
/// </para>
/// </summary>
public sealed class LearningApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string StorageBucket = "ums-learning-uploads-test";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("ums_learning_it")
        .WithUsername("ums")
        .WithPassword("ums_test_password")
        .Build();

    private readonly RedisContainer _redis = new RedisBuilder("redis:7-alpine")
        .Build();

    private readonly MinioContainer _minio = new MinioBuilder("minio/minio:RELEASE.2025-04-08T15-41-24Z")
        .WithUsername("minioadmin")
        .WithPassword("minioadmin")
        .Build();

    /// <summary>Exposes the real Postgres connection string so tests can verify rows Learning's own repositories don't expose (Audit's <c>audit_log_entries</c>, Learning's own <c>outbox_messages</c>) - mirrors Academic/Student/Faculty's own remarks.</summary>
    public string PostgresConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _redis.StartAsync(), _minio.StartAsync());

        PostgresConnectionString = _postgres.GetConnectionString();
        var minioEndpoint = _minio.GetConnectionString();

        var overrides = new Dictionary<string, string>
        {
            ["ConnectionStrings__Postgres"] = PostgresConnectionString,
            ["ConnectionStrings__Redis"] = _redis.GetConnectionString(),
            ["Jwt__Issuer"] = "https://identity.ums-suite.internal",
            ["Jwt__Audience"] = "ums-suite",
            ["Jwt__SigningKey"] = "integration-test-only-jwt-signing-key-not-for-real-use-32b",
            ["Identity__Argon2__MemorySizeKb"] = "8192",
            ["Identity__Argon2__Iterations"] = "1",
            ["Identity__Argon2__Parallelism"] = "1",
            ["Identity__Tokens__AccessTokenLifetime"] = "00:15:00",
            ["Identity__Tokens__RefreshTokenLifetime"] = "14.00:00:00",
            ["Identity__Tokens__RefreshReuseGraceWindow"] = "00:00:03",
            ["Audit__Storage__Endpoint"] = minioEndpoint,
            ["Audit__Storage__AccessKey"] = "minioadmin",
            ["Audit__Storage__SecretKey"] = "minioadmin",
            ["Audit__Storage__Bucket"] = "ums-learning-audit-test",
            ["Documents__Storage__Endpoint"] = minioEndpoint,
            ["Documents__Storage__AccessKey"] = "minioadmin",
            ["Documents__Storage__SecretKey"] = "minioadmin",
            ["Documents__Storage__Bucket"] = StorageBucket,
            ["Documents__Rendering__VerifyBaseUrl"] = "https://ums-suite.test/api/v1/documents/verify",
            ["Learning__PlagiarismProvider__FailureRate"] = "0",
            ["Learning__PlagiarismProvider__MinLatencyMs"] = "1",
            ["Learning__PlagiarismProvider__MaxLatencyMs"] = "5",
        };

        foreach (var (key, value) in overrides)
        {
            Environment.SetEnvironmentVariable(key, value);
        }

        // Forces the Host's DI container to build now, in isolation, rather than for the first
        // time mid-test.
        using var scope = Services.CreateScope();
        _ = scope.ServiceProvider.GetRequiredService<IConnectionMultiplexer>();

        await EnsureBucketExistsAsync(minioEndpoint, "ums-learning-audit-test").ConfigureAwait(false);
        await EnsureBucketExistsAsync(minioEndpoint, StorageBucket).ConfigureAwait(false);
    }

    public new async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await _redis.DisposeAsync();
        await _minio.DisposeAsync();
    }

    private static async Task EnsureBucketExistsAsync(string endpoint, string bucket)
    {
        using var client = new AmazonS3Client("minioadmin", "minioadmin", new AmazonS3Config
        {
            ServiceURL = endpoint,
            ForcePathStyle = true,
            UseHttp = true,
        });

        try
        {
            await client.PutBucketAsync(new PutBucketRequest { BucketName = bucket }).ConfigureAwait(false);
        }
        catch (BucketAlreadyOwnedByYouException)
        {
            // Idempotent by design - see DocumentsApiFixture's own remark for why this can run
            // more than once for the same fixture instance under VSTest.
        }
    }
}
