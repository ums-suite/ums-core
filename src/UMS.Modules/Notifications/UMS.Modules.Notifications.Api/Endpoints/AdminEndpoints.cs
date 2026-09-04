using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Notifications.Application.Admin;
using UMS.Modules.Notifications.Application.Permissions;
using UMS.Shared.Authorization;
using UMS.Shared.ErrorHandling;

namespace UMS.Modules.Notifications.Api.Endpoints;

/// <summary>NTF-14: admin/ops delivery-status and dead-letter triage (requirement-spec.md §6).</summary>
internal static class AdminEndpoints
{
    public static void MapAdminEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/requests/{id:guid}", async (Guid id, HttpContext httpContext, NotificationStatusQueryService service, CancellationToken cancellationToken) =>
        {
            var result = await service.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(NotificationsPermissions.RequestRead);

        group.MapGet("/dead-letters", async (int? skip, int? take, NotificationStatusQueryService service, CancellationToken cancellationToken) =>
        {
            var deadLetters = await service.ListDeadLettersAsync(skip ?? 0, take ?? 50, cancellationToken).ConfigureAwait(false);
            return Results.Ok(deadLetters);
        }).RequirePermission(NotificationsPermissions.DeadLetterRead);
    }
}
