using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace UMS.Modules.Identity.IntegrationTests.Infrastructure;

/// <summary>
/// Boots the real <c>UMS.Host</c> pipeline (Program.cs, unmodified) against real, disposable
/// Postgres/Redis containers - not mocks - so migrations, EF queries, Argon2id hashing, JWT
/// issuance, and Redis cache-aside/rate-limit code all run exactly as they would in production.
/// Argon2 cost is deliberately lowered (still real Argon2id, just cheaper) purely for suite
/// runtime - never done in a real environment.
///
/// Configuration overrides are set as process environment variables (double-underscore nested-key
/// syntax) rather than via <c>WebApplicationFactory.ConfigureWebHost</c>'s
/// <c>ConfigureAppConfiguration</c> hook: Program.cs is a top-level-statements minimal-hosting
/// entry point, and several of its own lines (e.g. connecting Redis) read configuration eagerly,
/// synchronously, before <c>WebApplicationBuilder.Build()</c> ever runs - earlier than the point in
/// the pipeline where that hook's overrides get spliced in. `WebApplication.CreateBuilder(args)`
/// itself adds environment variables as one of its first configuration sources, so setting them
/// here - before this fixture ever touches <see cref="WebApplicationFactory{TEntryPoint}.Services"/>
/// for the first time - makes them visible to every read in Program.cs, eager or lazy alike.
/// </summary>
public sealed class IdentityApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("ums_identity_it")
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

        // Forces the Host's DI container to build now, in isolation, rather than for the first
        // time mid-test.
        using var scope = Services.CreateScope();
        _ = scope.ServiceProvider.GetRequiredService<IConnectionMultiplexer>();
    }

    public new async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await _redis.DisposeAsync();
    }
}
