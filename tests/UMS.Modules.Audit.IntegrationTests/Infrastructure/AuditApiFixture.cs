using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using Testcontainers.Minio;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace UMS.Modules.Audit.IntegrationTests.Infrastructure;

/// <summary>
/// Boots the real <c>UMS.Host</c> pipeline against real, disposable Postgres/Redis containers -
/// mirrors Identity's own <c>IdentityApiFixture</c> exactly (see that class's remarks for why
/// configuration overrides are set as process environment variables rather than via
/// <c>ConfigureWebHost</c>). Both Identity's and Audit's modules are wired into the same Host, so
/// this fixture exercises the real cross-module <c>IAuditRecorder</c> write path (AUD-1) alongside
/// Audit's own read/export endpoints.
/// </summary>
public sealed class AuditApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("ums_audit_it")
        .WithUsername("ums")
        .WithPassword("ums_test_password")
        .Build();

    private readonly RedisContainer _redis = new RedisBuilder("redis:7-alpine")
        .Build();

    private readonly MinioContainer _minio = new MinioBuilder("minio/minio:RELEASE.2025-04-08T15-41-24Z")
        .WithUsername("minioadmin")
        .WithPassword("minioadmin")
        .Build();

    public const string StorageBucket = "ums-documents-test";

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
            ["Audit__Storage__Bucket"] = StorageBucket,
        };

        foreach (var (key, value) in overrides)
        {
            Environment.SetEnvironmentVariable(key, value);
        }

        // Forces the Host's DI container to build now, in isolation, rather than for the first
        // time mid-test.
        using var scope = Services.CreateScope();
        _ = scope.ServiceProvider.GetRequiredService<IConnectionMultiplexer>();

        await EnsureBucketExistsAsync(minioEndpoint).ConfigureAwait(false);
    }

    private static async Task EnsureBucketExistsAsync(string endpoint)
    {
        using var client = new AmazonS3Client("minioadmin", "minioadmin", new AmazonS3Config
        {
            ServiceURL = endpoint,
            ForcePathStyle = true,
            UseHttp = true,
        });

        await client.PutBucketAsync(new PutBucketRequest { BucketName = StorageBucket }).ConfigureAwait(false);
    }

    public new async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await _redis.DisposeAsync();
        await _minio.DisposeAsync();
    }
}
