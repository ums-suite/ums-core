using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using UMS.Modules.Finance.Application.Abstractions;
using UMS.Modules.Finance.Application.FeeStructures;
using UMS.Modules.Finance.Application.Invoices;
using UMS.Modules.Finance.Application.Payments;
using UMS.Modules.Finance.Infrastructure.Authorization;
using UMS.Modules.Finance.Infrastructure.Documents;
using UMS.Modules.Finance.Infrastructure.Gateway;
using UMS.Modules.Finance.Infrastructure.Invoices;
using UMS.Modules.Finance.Infrastructure.Notifications;
using UMS.Modules.Finance.Infrastructure.Persistence;
using UMS.Modules.Finance.Infrastructure.Persistence.Repositories;
using UMS.Shared.Authorization;
using UMS.Shared.Finance;
using UMS.Shared.Resilience.Http;

namespace UMS.Modules.Finance.Infrastructure;

/// <summary>
/// Composition root for the Finance module (release/DEVELOPMENT_PLAN.md Flow #14, "Finance -
/// Payment Core"). Mirrors every other module's own <c>DependencyInjection</c> exactly.
///
/// <para>
/// Cross-module dependencies (module-boundaries.md: Finance depends on Identity only - "Finance
/// intentionally has no outgoing domain dependency") are resolved against their real
/// implementations, already registered by the Host/Workers composition root before
/// <c>AddFinanceModule</c> runs: Documents via <c>UMS.Shared.Documents.IDocumentGenerationRequester</c>
/// (FIN-15), Notifications via <c>UMS.Shared.Notifications.INotificationRequestIntake</c> (FIN-16),
/// and Audit via <c>UMS.Shared.Audit.IAuditRecorder</c> (FIN-17). There is no stub anywhere in this
/// module - every contract it needs already has a real implementation, the same "last module in its
/// own dependency chain" posture Learning's own DependencyInjection remarks describe.
/// </para>
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddFinanceModule(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddDbContext<FinanceDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("Postgres")
                    ?? throw new InvalidOperationException("Missing required 'ConnectionStrings:Postgres' configuration value."),
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", "finance")));

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<FinanceDbContext>());
        services.AddScoped<IDomainEventRecorder>(sp => sp.GetRequiredService<FinanceDbContext>());

        services.AddScoped<IFeeStructureRepository, FeeStructureRepository>();
        services.AddScoped<IInvoiceRepository, InvoiceRepository>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<ILedgerEntryRepository, LedgerEntryRepository>();
        services.AddScoped<IOutboxReader, OutboxReader>();

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IPermissionManifest, FinancePermissionManifest>();

        services.AddScoped<IReceiptRequester, ReceiptRequesterAdapter>();
        services.AddScoped<INotificationRequestPublisher, NotificationRequestIntakeAdapter>();
        services.AddScoped<IInvoiceRequester, InvoiceRequesterAdapter>();

        RegisterFakePaymentGateway(services, configuration);

        services.AddScoped<FeeStructureService>();
        services.AddScoped<InvoiceService>();
        services.AddScoped<PaymentService>();
        services.AddScoped<PaymentWebhookService>();
        services.AddScoped<StuckPaymentSweepService>();

        return services;
    }

    /// <summary>Applies pending EF Core migrations for the <c>finance</c> schema - called once from the Host/Workers composition root, mirroring every other module's own <c>UseXModuleAsync</c>.</summary>
    public static async Task UseFinanceModuleAsync(this IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
        await dbContext.Database.MigrateAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// FIN-4: registers SSLCommerz's typed <c>HttpClient</c> with <c>UMS.Shared.Resilience</c>'s
    /// standard Polly pipeline (retry with exponential backoff + jitter, circuit breaker, timeout)
    /// wrapped around <see cref="FakeSslCommerzPrimaryHandler"/> as the innermost (primary) handler -
    /// see that class's own remarks for why this build fakes the gateway, and Notifications'
    /// <c>RegisterFakeChannelGateways</c> for the pattern this mirrors.
    /// </summary>
    private static void RegisterFakePaymentGateway(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<FakePaymentGatewayOptions>(configuration.GetSection("Finance:PaymentGateway"));
        services.AddSingleton<FakeSslCommerzGatewayState>();

        services.AddUmsResilientHttpClient<SslCommerzPaymentGateway>()
            .ConfigureHttpClient(client => client.BaseAddress = new Uri("https://fake-sslcommerz-gateway.ums-suite.internal/"))
            .ConfigurePrimaryHttpMessageHandler(sp => new FakeSslCommerzPrimaryHandler(sp.GetRequiredService<FakeSslCommerzGatewayState>(), sp.GetRequiredService<IOptionsMonitor<FakePaymentGatewayOptions>>()));

        services.AddScoped<IPaymentGateway>(sp => sp.GetRequiredService<SslCommerzPaymentGateway>());
    }
}
