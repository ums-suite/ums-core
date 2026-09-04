using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using UMS.Modules.Notifications.Application.Abstractions;
using UMS.Modules.Notifications.Application.Admin;
using UMS.Modules.Notifications.Application.Dispatch;
using UMS.Modules.Notifications.Application.InApp;
using UMS.Modules.Notifications.Application.Preferences;
using UMS.Modules.Notifications.Application.Requests;
using UMS.Modules.Notifications.Application.Templates;
using UMS.Modules.Notifications.Application.Webhooks;
using UMS.Modules.Notifications.Domain.Common;
using UMS.Modules.Notifications.Infrastructure.Authorization;
using UMS.Modules.Notifications.Infrastructure.Persistence;
using UMS.Modules.Notifications.Infrastructure.Persistence.Repositories;
using UMS.Modules.Notifications.Infrastructure.Providers;
using UMS.Modules.Notifications.Infrastructure.RateLimiting;
using UMS.Shared.Authorization;
using UMS.Shared.Notifications;
using UMS.Shared.Resilience.Http;

namespace UMS.Modules.Notifications.Infrastructure;

/// <summary>Composition root for the Notifications module - mirrors Audit's/Identity's own <c>DependencyInjection</c> exactly.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddNotificationsModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<NotificationsDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("Postgres")
                    ?? throw new InvalidOperationException("Missing required 'ConnectionStrings:Postgres' configuration value."),
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", "notifications")));

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<NotificationsDbContext>());
        services.AddScoped<IDomainEventRecorder>(sp => sp.GetRequiredService<NotificationsDbContext>());

        services.AddScoped<INotificationRequestRepository, NotificationRequestRepository>();
        services.AddScoped<INotificationDeliveryAttemptRepository, NotificationDeliveryAttemptRepository>();
        services.AddScoped<ITemplateRepository, TemplateRepository>();
        services.AddScoped<IRecipientPreferenceRepository, RecipientPreferenceRepository>();
        services.AddScoped<IChannelSuppressionRepository, ChannelSuppressionRepository>();

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IOtpRateLimiter, OtpRateLimiter>();

        // NTF-3: the cross-module inbound contract every other module's own outbox relay calls
        // in-process (see UMS.Shared.Notifications.INotificationRequestIntake's own remarks).
        services.AddScoped<INotificationRequestIntake, SubmitNotificationRequestService>();

        RegisterFakeChannelGateways(services, configuration);

        services.AddScoped<NotificationDispatchService>();
        services.AddScoped<InAppNotificationQueryService>();
        services.AddScoped<TemplateManagementService>();
        services.AddScoped<RecipientPreferenceService>();
        services.AddScoped<NotificationStatusQueryService>();
        services.AddScoped<ProviderWebhookService>();

        services.AddSingleton<IPermissionManifest, NotificationsPermissionManifest>();

        return services;
    }

    /// <summary>Applies pending EF Core migrations for the <c>notifications</c> schema - called once from the Host composition root, mirroring Audit's own <c>UseAuditModuleAsync</c>.</summary>
    public static async Task UseNotificationsModuleAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
        await dbContext.Database.MigrateAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// NTF-9/10/11 + WhatsApp: registers each channel's typed <c>HttpClient</c> with
    /// <c>UMS.Shared.Resilience</c>'s standard Polly pipeline (ums-conventions.md, Resilience &amp;
    /// Reliability) wrapped around <see cref="FakeProviderPrimaryHandler"/> as the innermost
    /// (primary) handler - see that class's own remarks for why this build fakes the provider
    /// rather than a real gateway. The resulting typed clients are exposed to
    /// <see cref="NotificationDispatchService"/> as one <see cref="IChannelProvider"/>-keyed
    /// dictionary rather than four separate constructor parameters.
    /// </summary>
    private static void RegisterFakeChannelGateways(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<FakeProviderGatewayOptions>("Email", configuration.GetSection("Notifications:FakeGateway:Email"));
        services.Configure<FakeProviderGatewayOptions>("Sms", configuration.GetSection("Notifications:FakeGateway:Sms"));
        services.Configure<FakeProviderGatewayOptions>("Push", configuration.GetSection("Notifications:FakeGateway:Push"));
        services.Configure<FakeProviderGatewayOptions>("WhatsApp", configuration.GetSection("Notifications:FakeGateway:WhatsApp"));

        AddFakeGatewayClient<HttpEmailChannelProvider>(services, "Email");
        AddFakeGatewayClient<HttpSmsChannelProvider>(services, "Sms");
        AddFakeGatewayClient<HttpPushChannelProvider>(services, "Push");
        AddFakeGatewayClient<HttpWhatsAppChannelProvider>(services, "WhatsApp");

        services.AddScoped<IReadOnlyDictionary<NotificationChannel, IChannelProvider>>(sp => new Dictionary<NotificationChannel, IChannelProvider>
        {
            [NotificationChannel.Email] = sp.GetRequiredService<HttpEmailChannelProvider>(),
            [NotificationChannel.Sms] = sp.GetRequiredService<HttpSmsChannelProvider>(),
            [NotificationChannel.Push] = sp.GetRequiredService<HttpPushChannelProvider>(),
            [NotificationChannel.WhatsApp] = sp.GetRequiredService<HttpWhatsAppChannelProvider>(),
        });
    }

    private static void AddFakeGatewayClient<TClient>(IServiceCollection services, string channelName)
        where TClient : class
    {
        services.AddUmsResilientHttpClient<TClient>()
            .ConfigureHttpClient(client => client.BaseAddress = new Uri($"https://fake-{channelName.ToLowerInvariant()}-gateway.ums-suite.internal/"))
            .ConfigurePrimaryHttpMessageHandler(sp => new FakeProviderPrimaryHandler(channelName, sp.GetRequiredService<IOptionsMonitor<FakeProviderGatewayOptions>>()));
    }
}
