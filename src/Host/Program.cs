using HealthChecks.NpgSql;
using HealthChecks.Redis;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using StackExchange.Redis;
using UMS.Modules.Audit.Api;
using UMS.Modules.Audit.Infrastructure;
using UMS.Modules.Documents.Api;
using UMS.Modules.Documents.Infrastructure;
using UMS.Modules.Identity.Api;
using UMS.Modules.Identity.Infrastructure;
using UMS.Modules.Organization.Api;
using UMS.Modules.Organization.Infrastructure;
using UMS.Shared.Authorization;
using UMS.Shared.ErrorHandling;
using UMS.Shared.Observability;
using UMS.Shared.Resilience;
using UMS.Shared.Resilience.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.AddUmsObservability(serviceName: "ums-core");

builder.Services.AddUmsErrorHandling();
await builder.Services.AddUmsResilienceAsync(builder.Configuration);

// Identity (release/DEVELOPMENT_PLAN.md Flow #4) is the first business module - every later
// module adds its own AddXModule(builder.Configuration) call here the same way.
builder.Services.AddIdentityModule(builder.Configuration);

// Audit (release/DEVELOPMENT_PLAN.md Flow #5) - depends on no other module (module-boundaries.md);
// every other module resolves its cross-module write path via UMS.Shared.Audit.IAuditRecorder,
// registered here.
builder.Services.AddAuditModule(builder.Configuration);

// Organization (release/DEVELOPMENT_PLAN.md Flow #6) - depends only on Identity for its own
// endpoint authz (module-boundaries.md); other modules resolve its cross-module read path via
// UMS.Shared.Organization.IOrganizationNodeExistenceChecker, registered here (Identity's own
// former stub registration now resolves this instead - see Identity.Infrastructure's
// DependencyInjection.cs).
builder.Services.AddOrganizationModule(builder.Configuration);

// Documents (release/DEVELOPMENT_PLAN.md Flow #9) - depends only on Identity (module-boundaries.md);
// calls back into Audit's IAuditRecorder (DOC-14, already registered above) for official-record
// document types.
builder.Services.AddDocumentsModule(builder.Configuration);

// Shared JWT authentication + permission-based authorization (ums-conventions.md: one shared
// implementation, not per-module reinvention) - every module's protected endpoints gate through
// this, never their own hand-rolled [Authorize] policy.
builder.Services.AddUmsAuthentication(builder.Configuration);
builder.Services.AddUmsAuthorization();

builder.Services.AddOpenApi();

// DOC-9: the public verify endpoint is rate-limited per ums-requirements.md §11 - keyed by client
// IP via a Redis-backed fixed window (ums-conventions.md, Resilience & Reliability: "Redis-backed
// specifically because ums-core runs N replicas"), registered once here rather than per-module,
// since ASP.NET Core's rate-limiter middleware/policy registry is itself a single, app-wide
// composition-root concern (mirrors how authentication/authorization are registered once above).
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("document-verify", (HttpContext httpContext) =>
    {
        var redis = httpContext.RequestServices.GetRequiredService<IConnectionMultiplexer>();
        var partitionKey = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return UmsRedisRateLimiterFactory.CreatePartition(redis, "document-verify", partitionKey, permitLimit: 30, window: TimeSpan.FromMinutes(1));
    });
});

// Postgres readiness check today verifies raw connectivity only - each module adds its own
// dependency-specific readiness check (ums-conventions.md, Observability) once it exists and owns
// a schema/DbContext to check against.
builder.Services.AddHealthChecks()
    .AddNpgSql(
        builder.Configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Missing required 'ConnectionStrings:Postgres' configuration value."),
        name: "postgres",
        tags: ["ready"])
    .AddRedis(
        builder.Configuration.GetConnectionString("Redis")
            ?? throw new InvalidOperationException("Missing required 'ConnectionStrings:Redis' configuration value."),
        name: "redis",
        tags: ["ready"]);

var app = builder.Build();

// Applies each module's pending EF Core migrations and syncs the Permission catalog - see
// AddIdentityModule's own UseIdentityModuleAsync remarks. Every later module adds its own
// await line here the same way.
await app.Services.UseIdentityModuleAsync();
await app.Services.UseAuditModuleAsync();
await app.Services.UseOrganizationModuleAsync();
await app.Services.UseDocumentsModuleAsync();

app.UseUmsObservability();
app.UseUmsErrorHandling();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

// Liveness: process is up, no dependency checks - governs whether Kubernetes restarts a wedged
// pod. Readiness: Postgres + Redis reachable - governs whether Kubernetes routes traffic to this
// pod (ums-conventions.md, Observability).
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

// src/UMS.Modules/<Module> endpoints register themselves under /api/v1/<module>/..., starting
// with Identity (release/DEVELOPMENT_PLAN.md Flow #4).
app.MapIdentityModule();
app.MapAuditModule();
app.MapOrganizationModule();
app.MapDocumentsModule();

app.Run();

/// <summary>Entry point type, exposed for WebApplicationFactory-based integration tests.</summary>
public partial class Program;
