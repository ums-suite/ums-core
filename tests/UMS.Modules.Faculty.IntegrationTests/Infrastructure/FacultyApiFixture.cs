using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace UMS.Modules.Faculty.IntegrationTests.Infrastructure;

/// <summary>Boots the real <c>UMS.Host</c> pipeline against real, disposable Postgres/Redis containers - mirrors <c>OrganizationApiFixture</c> exactly.</summary>
public sealed class FacultyApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("ums_faculty_it")
        .WithUsername("ums")
        .WithPassword("ums_test_password")
        .Build();

    private readonly RedisContainer _redis = new RedisBuilder("redis:7-alpine")
        .Build();

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _redis.StartAsync());

        var overrides = new Dictionary<string, string>
        {
            ["ConnectionStrings__Postgres"] = _postgres.GetConnectionString(),
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
        };

        foreach (var (key, value) in overrides)
        {
            Environment.SetEnvironmentVariable(key, value);
        }

        using var scope = Services.CreateScope();
        _ = scope.ServiceProvider.GetRequiredService<IConnectionMultiplexer>();
    }

    /// <summary>Exposes the real Postgres connection string so tests can seed a fixture row directly into a table Faculty's own repositories don't own (e.g. a fabricated <c>academic.outbox_messages</c> row for FAC-4's projection consumer, since Academic doesn't exist yet - see <c>CourseAssignmentProjectionTests</c>'s own remarks).</summary>
    public string PostgresConnectionString => _postgres.GetConnectionString();

    public new async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await _redis.DisposeAsync();
    }
}
