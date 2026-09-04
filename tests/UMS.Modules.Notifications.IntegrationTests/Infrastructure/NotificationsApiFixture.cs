using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace UMS.Modules.Notifications.IntegrationTests.Infrastructure;

/// <summary>
/// Boots the real <c>UMS.Host</c> pipeline against real, disposable Postgres/Redis containers -
/// mirrors Audit's own <c>AuditApiFixture</c> exactly (see that class's remarks). Identity's module
/// is wired into the same Host, so this fixture exercises Notifications' real cross-module
/// <c>IRecipientDirectory</c>/<c>IAuditRecorder</c> read/write paths alongside its own endpoints.
///
/// <para>
/// The fake channel gateway's failure rate is pinned to 0 here (deterministic pass/fail assertions).
/// The one test that needs a forced provider outage (edge-cases.md's "SMS provider outage") builds
/// its own derived host via <see cref="Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory{TEntryPoint}.WithWebHostBuilder"/>
/// layering a <c>ForceOutage=true</c> override on top of this same fixture's already-running
/// containers, rather than standing up a second Postgres/Redis pair.
/// </para>
/// </summary>
public sealed class NotificationsApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("ums_notifications_it")
        .WithUsername("ums")
        .WithPassword("ums_test_password")
        .Build();

    private readonly RedisContainer _redis = new RedisBuilder("redis:7-alpine")
        .Build();

    public string PostgresConnectionString { get; private set; } = string.Empty;

    public string RedisConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _redis.StartAsync());

        PostgresConnectionString = _postgres.GetConnectionString();
        RedisConnectionString = _redis.GetConnectionString();

        foreach (var (key, value) in BuildEnvironmentOverrides(PostgresConnectionString, RedisConnectionString))
        {
            Environment.SetEnvironmentVariable(key, value);
        }

        // Forces the Host's DI container to build now, in isolation, rather than for the first time
        // mid-test (mirrors AuditApiFixture's own remarks on why).
        using var scope = Services.CreateScope();
        _ = scope.ServiceProvider.GetRequiredService<IConnectionMultiplexer>();
    }

    public new async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await _redis.DisposeAsync();
    }

    internal static Dictionary<string, string> BuildEnvironmentOverrides(string postgresConnectionString, string redisConnectionString) => new()
    {
        ["ConnectionStrings__Postgres"] = postgresConnectionString,
        ["ConnectionStrings__Redis"] = redisConnectionString,
        ["Jwt__Issuer"] = "https://identity.ums-suite.internal",
        ["Jwt__Audience"] = "ums-suite",
        ["Jwt__SigningKey"] = "integration-test-only-jwt-signing-key-not-for-real-use-32b",
        ["Identity__Argon2__MemorySizeKb"] = "8192",
        ["Identity__Argon2__Iterations"] = "1",
        ["Identity__Argon2__Parallelism"] = "1",
        ["Identity__Tokens__AccessTokenLifetime"] = "00:15:00",
        ["Identity__Tokens__RefreshTokenLifetime"] = "14.00:00:00",
        ["Identity__Tokens__RefreshReuseGraceWindow"] = "00:00:03",

        // Deterministic fake gateway behavior for this suite's assertions (see this class's own
        // remarks) - NTF-13's real retry/dead-letter machinery is separately exercised against a
        // forced 100% failure rate by ForceOutageFixture, not by randomness here.
        ["Notifications__FakeGateway__Email__FailureRate"] = "0",
        ["Notifications__FakeGateway__Sms__FailureRate"] = "0",
        ["Notifications__FakeGateway__Push__FailureRate"] = "0",
        ["Notifications__FakeGateway__WhatsApp__FailureRate"] = "0",
        ["Notifications__FakeGateway__Email__MinLatencyMs"] = "1",
        ["Notifications__FakeGateway__Email__MaxLatencyMs"] = "5",
        ["Notifications__FakeGateway__Sms__MinLatencyMs"] = "1",
        ["Notifications__FakeGateway__Sms__MaxLatencyMs"] = "5",
    };
}
