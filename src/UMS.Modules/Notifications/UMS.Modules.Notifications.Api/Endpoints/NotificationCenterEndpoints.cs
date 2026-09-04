using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Notifications.Application.InApp;
using UMS.Shared.ErrorHandling;

namespace UMS.Modules.Notifications.Api.Endpoints;

/// <summary>NTF-12: the in-app notification center's own read API (requirement-spec.md §6).</summary>
internal static class NotificationCenterEndpoints
{
    public static void MapNotificationCenterEndpoints(this RouteGroupBuilder group)
    {
        var me = group.MapGroup("/me").RequireAuthorization();

        me.MapGet("/", async (int? skip, int? take, HttpContext httpContext, InAppNotificationQueryService service, CancellationToken cancellationToken) =>
        {
            var notifications = await service.ListAsync(httpContext.User.GetUserId(), skip ?? 0, take ?? 20, cancellationToken).ConfigureAwait(false);
            return Results.Ok(notifications);
        });

        me.MapGet("/unread-count", async (HttpContext httpContext, InAppNotificationQueryService service, CancellationToken cancellationToken) =>
        {
            var count = await service.UnreadCountAsync(httpContext.User.GetUserId(), cancellationToken).ConfigureAwait(false);
            return Results.Ok(new { unreadCount = count });
        });

        me.MapPatch("/{id:guid}/read", async (Guid id, HttpContext httpContext, InAppNotificationQueryService service, CancellationToken cancellationToken) =>
        {
            var result = await service.MarkReadAsync(httpContext.User.GetUserId(), id, cancellationToken).ConfigureAwait(false);
            return result.IsSuccess ? Results.NoContent() : result.Error!.ToProblemResult(httpContext);
        });

        me.MapPatch("/read-all", async (HttpContext httpContext, InAppNotificationQueryService service, CancellationToken cancellationToken) =>
        {
            await service.MarkAllReadAsync(httpContext.User.GetUserId(), cancellationToken).ConfigureAwait(false);
            return Results.NoContent();
        });
    }
}
