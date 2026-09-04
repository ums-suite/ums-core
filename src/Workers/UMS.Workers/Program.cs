using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using UMS.Modules.Audit.Infrastructure;
using UMS.Shared.Observability;
using UMS.Workers;
using UMS.Workers.AuditExports;

var builder = WebApplication.CreateBuilder(args);

builder.AddUmsObservability(serviceName: "ums-workers");

builder.Services.AddHostedService<Worker>();

// Audit's export relay (AUD-9/AUD-10, ADR-0014) - the first real worker to run on this host.
// Registers the same module DI (repositories, IObjectStorage, IOutboxReader) the Host uses for
// Audit's own read/write path, since both processes share one physical Postgres database
// (ADR-0001).
builder.Services.AddAuditModule(builder.Configuration);
builder.Services.AddHostedService<AuditExportRelayWorker>();

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

// Idempotent (CREATE TABLE IF NOT EXISTS partitions; EF's own migrations-history check) - safe to
// also run from this second process regardless of whichever of UMS.Host/UMS.Workers happens to
// start first (ADR-0001's single physical database).
await app.Services.UseAuditModuleAsync();

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

app.Run();
