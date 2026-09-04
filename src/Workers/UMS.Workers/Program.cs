using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using UMS.Modules.Audit.Infrastructure;
using UMS.Modules.Documents.Infrastructure;
using UMS.Modules.Identity.Infrastructure;
using UMS.Modules.Notifications.Infrastructure;
using UMS.Shared.Observability;
using UMS.Shared.Resilience;
using UMS.Workers;
using UMS.Workers.AuditExports;
using UMS.Workers.BulkDocumentGeneration;
using UMS.Workers.Notifications;

var builder = WebApplication.CreateBuilder(args);

builder.AddUmsObservability(serviceName: "ums-workers");

builder.Services.AddHostedService<Worker>();

// Audit's export relay (AUD-9/AUD-10, ADR-0014) - the first real worker to run on this host.
// Registers the same module DI (repositories, IObjectStorage, IOutboxReader) the Host uses for
// Audit's own read/write path, since both processes share one physical Postgres database
// (ADR-0001).
builder.Services.AddAuditModule(builder.Configuration);
builder.Services.AddHostedService<AuditExportRelayWorker>();

// Documents' bulk/async generation path (DOC-4/DOC-6/DOC-7), the object-storage-outage retry
// relay (DOC-15), and the compensating stale-claim sweep (edge-cases.md's object-storage/DB-
// ordering decision) - the three background pieces Documents needs on this host, mirroring
// Audit's own export relay registration immediately above.
builder.Services.AddDocumentsModule(builder.Configuration);
builder.Services.AddHostedService<BulkGenerationRelayWorker>();
builder.Services.AddHostedService<DocumentGenerationRetryRelayWorker>();
builder.Services.AddHostedService<PendingDocumentSweepWorker>();

// UMS.Shared.Resilience's Redis multiplexer (ADR-0007) - Notifications' OTP rate limiter and every
// channel provider's Polly-wrapped HttpClient (NTF-9/10/11 + WhatsApp) both need it.
await builder.Services.AddUmsResilienceAsync(builder.Configuration);

// Identity registered here only so Notifications' own UMS.Shared.Identity.IRecipientDirectory
// cross-module read path (NTF-2) has a real implementation to resolve in THIS process too - this
// worker process never serves an HTTP request of its own, so none of Identity's authentication/
// authorization wiring is exercised here.
builder.Services.AddIdentityModule(builder.Configuration);

// Notifications' per-channel dispatch workers (NTF-13's retry/dead-letter loop, NTF-16's
// bulk/backpressure-aware prioritized dispatch) - one independent BackgroundService per channel so
// a backed-up channel (edge-cases.md's "SMS provider outage") never delays another channel's own
// dispatch (ADR-0009). Also gives Documents' DOC-13 call into
// UMS.Shared.Notifications.INotificationRequestIntake a real implementation to resolve when the
// bulk generation path runs from this process.
builder.Services.AddNotificationsModule(builder.Configuration);
builder.Services.AddHostedService<EmailDispatchWorker>();
builder.Services.AddHostedService<SmsDispatchWorker>();
builder.Services.AddHostedService<WhatsAppDispatchWorker>();
builder.Services.AddHostedService<PushDispatchWorker>();
builder.Services.AddHostedService<InAppDispatchWorker>();

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
await app.Services.UseDocumentsModuleAsync();
await app.Services.UseIdentityModuleAsync();
await app.Services.UseNotificationsModuleAsync();

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

app.Run();
