using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using UMS.Shared.Observability;
using UMS.Workers;

var builder = WebApplication.CreateBuilder(args);

builder.AddUmsObservability(serviceName: "ums-workers");

builder.Services.AddHostedService<Worker>();

// Same readiness contract as UMS.Host (ums-conventions.md, Observability: "UMS.Workers exposes
// the same two endpoints"). Per-job outbox/queue-depth checks (ADR-0014) are added once the first
// real worker (module-owned outbox relay) exists.
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

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

app.Run();
