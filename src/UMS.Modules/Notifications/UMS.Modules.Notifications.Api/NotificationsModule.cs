using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using UMS.Modules.Notifications.Api.Endpoints;

namespace UMS.Modules.Notifications.Api;

/// <summary>Notifications' endpoints register themselves under <c>/api/v1/notifications/...</c>, mirroring Audit's/Identity's own module registrars. The Host calls this one extension - it never maps a Notifications route directly itself.</summary>
public static class NotificationsModule
{
    public static IEndpointRouteBuilder MapNotificationsModule(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/notifications").WithTags("Notifications");

        group.MapNotificationCenterEndpoints();
        group.MapTemplateEndpoints();
        group.MapAdminEndpoints();
        group.MapWebhookEndpoints();

        var environment = endpoints.ServiceProvider.GetRequiredService<IHostEnvironment>();
        if (environment.IsDevelopment())
        {
            group.MapDevOnlyTestSubmitEndpoint();
        }

        return endpoints;
    }
}
