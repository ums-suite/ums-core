using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Notifications.Api.Contracts;
using UMS.Modules.Notifications.Application.Webhooks;
using UMS.Modules.Notifications.Domain.Common;

namespace UMS.Modules.Notifications.Api.Endpoints;

/// <summary>NTF-15: provider bounce/delivery-receipt callbacks (requirement-spec.md §6). Unauthenticated by design - a real provider webhook calls this from outside the platform, matching every other provider-webhook convention (signature/shared-secret verification is a real gateway's own concern, left as a documented gap since this build's gateway is a fake per <see cref="ProviderWebhookService"/>'s own remarks).</summary>
internal static class WebhookEndpoints
{
    public static void MapWebhookEndpoints(this RouteGroupBuilder group)
    {
        var webhooks = group.MapGroup("/webhooks").AllowAnonymous();

        webhooks.MapPost("/email", async (ProviderWebhookRequestBody body, ProviderWebhookService service, CancellationToken cancellationToken) =>
        {
            await service.HandleAsync(NotificationChannel.Email, new ProviderWebhookPayload(body.ProviderMessageId, body.Event, body.Reason), cancellationToken).ConfigureAwait(false);
            return Results.Ok();
        });

        webhooks.MapPost("/sms", async (ProviderWebhookRequestBody body, ProviderWebhookService service, CancellationToken cancellationToken) =>
        {
            await service.HandleAsync(NotificationChannel.Sms, new ProviderWebhookPayload(body.ProviderMessageId, body.Event, body.Reason), cancellationToken).ConfigureAwait(false);
            return Results.Ok();
        });
    }
}
