using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Events;
using UMS.Shared.Observability.Correlation;

namespace UMS.Shared.Observability;

/// <summary>
/// One shared registration for structured logging + tracing/metrics (ums-conventions.md,
/// Observability: "wired once per module via a single DI registration - no module hand-rolls its
/// own logging/tracing setup"). Every deployable in this repo (UMS.Host, UMS.Workers) calls
/// <see cref="AddUmsObservability"/> once during startup instead of configuring Serilog/OpenTelemetry
/// itself.
/// </summary>
public static class DependencyInjection
{
    public static WebApplicationBuilder AddUmsObservability(this WebApplicationBuilder builder, string serviceName)
    {
        builder.Host.UseSerilog((context, services, loggerConfiguration) =>
        {
            loggerConfiguration
                .ReadFrom.Configuration(context.Configuration)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("service", serviceName)

                // JSON console is the primary sink everywhere (local Docker Compose included) -
                // what the Alloy/Promtail collector tails into Loki, so local and production
                // logging behave identically (ums-conventions.md, Observability).
                .WriteTo.Console(new Serilog.Formatting.Json.JsonFormatter());

            if (context.HostingEnvironment.IsDevelopment())
            {
                // Rolling-file sink for a developer reading logs without the LGTM stack running -
                // never relied on in a deployed environment (container filesystems are ephemeral
                // and ums-core runs N replicas).
                loggerConfiguration.WriteTo.File(
                    string.Create(CultureInfo.InvariantCulture, $"logs/{serviceName}-.log"),
                    rollingInterval: RollingInterval.Day,
                    restrictedToMinimumLevel: LogEventLevel.Debug,
                    formatProvider: CultureInfo.InvariantCulture);
            }
        });

        var otlpEndpoint = builder.Configuration["Observability:OtlpEndpoint"] ?? "http://localhost:4317";

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddOtlpExporter(otlp => otlp.Endpoint = new Uri(otlpEndpoint)))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddOtlpExporter(otlp => otlp.Endpoint = new Uri(otlpEndpoint)));

        return builder;
    }

    /// <summary>Wires the correlation-id middleware and Serilog's per-request summary log line.</summary>
    public static WebApplication UseUmsObservability(this WebApplication app)
    {
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseSerilogRequestLogging();
        return app;
    }
}
