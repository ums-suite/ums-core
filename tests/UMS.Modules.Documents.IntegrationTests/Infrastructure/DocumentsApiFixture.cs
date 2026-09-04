using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using Testcontainers.Minio;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace UMS.Modules.Documents.IntegrationTests.Infrastructure;

/// <summary>
/// Boots the real <c>UMS.Host</c> pipeline against real, disposable Postgres/Redis/MinIO containers
/// - mirrors Audit's own <c>AuditApiFixture</c> exactly (see that class's remarks for why
/// configuration overrides are set as process environment variables rather than via
/// <c>ConfigureWebHost</c>). Identity's and Documents' modules are both wired into the same Host,
/// so this fixture exercises the real cross-module <c>IAuditRecorder</c> write path (DOC-14)
/// alongside Documents' own generate/verify/upload endpoints.
/// </summary>
public sealed class DocumentsApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("ums_documents_it")
        .WithUsername("ums")
        .WithPassword("ums_test_password")
        .Build();

    private readonly RedisContainer _redis = new RedisBuilder("redis:7-alpine")
        .Build();

    private readonly MinioContainer _minio = new MinioBuilder("minio/minio:RELEASE.2025-04-08T15-41-24Z")
        .WithUsername("minioadmin")
        .WithPassword("minioadmin")
        .Build();

    public const string StorageBucket = "ums-generated-documents-test";

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
            ["Audit__Storage__Bucket"] = "ums-documents-test",
            ["Documents__Storage__Endpoint"] = minioEndpoint,
            ["Documents__Storage__AccessKey"] = "minioadmin",
            ["Documents__Storage__SecretKey"] = "minioadmin",
            ["Documents__Storage__Bucket"] = StorageBucket,
            ["Documents__Rendering__VerifyBaseUrl"] = "https://ums-suite.test/api/v1/documents/verify",
        };

        foreach (var (key, value) in overrides)
        {
            Environment.SetEnvironmentVariable(key, value);
        }

        // Forces the Host's DI container to build now, in isolation, rather than for the first
        // time mid-test.
        using var scope = Services.CreateScope();
        _ = scope.ServiceProvider.GetRequiredService<IConnectionMultiplexer>();

        await EnsureBucketExistsAsync(minioEndpoint, "ums-documents-test").ConfigureAwait(false);
        await EnsureBucketExistsAsync(minioEndpoint, StorageBucket).ConfigureAwait(false);
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
            // Idempotent by design - observed in practice when running this suite alongside other
            // Testcontainers-backed suites in one `dotnet test` invocation across the whole
            // solution, where IAsyncLifetime.InitializeAsync can run more than once for the same
            // fixture instance depending on VSTest's own test-host scheduling.
        }
    }

    public new async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await _redis.DisposeAsync();
        await _minio.DisposeAsync();
    }
}
