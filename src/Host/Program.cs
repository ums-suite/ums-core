using HealthChecks.NpgSql;
using HealthChecks.Redis;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using UMS.Shared.ErrorHandling;
using UMS.Shared.Observability;
using UMS.Shared.Resilience;

var builder = WebApplication.CreateBuilder(args);

builder.AddUmsObservability(serviceName: "ums-core");

builder.Services.AddUmsErrorHandling();
builder.Services.AddUmsResilience(builder.Configuration);

builder.Services.AddOpenApi();

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

app.UseUmsObservability();
app.UseUmsErrorHandling();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Liveness: process is up, no dependency checks - governs whether Kubernetes restarts a wedged
// pod. Readiness: Postgres + Redis reachable - governs whether Kubernetes routes traffic to this
// pod (ums-conventions.md, Observability).
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

// src/UMS.Modules/<Module> endpoints register themselves under /api/v1/<module>/... starting with
// Identity (release/DEVELOPMENT_PLAN.md Flow #4) - nothing to map here yet.
app.Run();

/// <summary>Entry point type, exposed for WebApplicationFactory-based integration tests.</summary>
public partial class Program;
