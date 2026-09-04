using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Notifications.Api.Contracts;
using UMS.Modules.Notifications.Application.Permissions;
using UMS.Modules.Notifications.Application.Templates;
using UMS.Shared.Authorization;
using UMS.Shared.ErrorHandling;

namespace UMS.Modules.Notifications.Api.Endpoints;

/// <summary>NTF-8: admin template list/manage endpoints (requirement-spec.md §6).</summary>
internal static class TemplateEndpoints
{
    public static void MapTemplateEndpoints(this RouteGroupBuilder group)
    {
        var templates = group.MapGroup("/templates").RequirePermission(NotificationsPermissions.TemplateManage);

        templates.MapGet("/", async (int? skip, int? take, TemplateManagementService service, CancellationToken cancellationToken) =>
        {
            var result = await service.ListAsync(skip ?? 0, take ?? 50, cancellationToken).ConfigureAwait(false);
            return Results.Ok(result);
        });

        templates.MapPut("/{id:guid}", async (Guid id, UpsertTemplateTranslationRequestBody body, HttpContext httpContext, TemplateManagementService service, CancellationToken cancellationToken) =>
        {
            var command = new UpsertTemplateTranslationCommand(body.LanguageCode, body.Subject, body.Body, body.PushTitle, body.DeepLink);
            var result = await service.UpdateAsync(id, command, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        });
    }
}
