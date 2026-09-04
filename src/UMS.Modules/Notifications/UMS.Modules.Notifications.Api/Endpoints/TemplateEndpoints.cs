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

        // requirement-spec.md §9 Open Questions: templates are "a data-driven, admin-configurable
        // registry" - this is the create side of that registry (get-or-create by natural key, so a
        // repeat call for the same (eventType, channel) is idempotent, never a duplicate row). Not
        // one of §6's two literal endpoint rows, the same kind of narrow, directly-authorized
        // extension Documents' hard-delete endpoints made for their own module (design-decisions.md
        // pattern) - without it, PUT /{id}'s "update an existing template's translations" contract
        // has no way to ever learn a new template's id in the first place.
        templates.MapPost("/", async (CreateTemplateRequestBody body, HttpContext httpContext, TemplateManagementService service, CancellationToken cancellationToken) =>
        {
            var result = await service.GetOrCreateAsync(body.EventType, body.Channel, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        });

        templates.MapPut("/{id:guid}", async (Guid id, UpsertTemplateTranslationRequestBody body, HttpContext httpContext, TemplateManagementService service, CancellationToken cancellationToken) =>
        {
            var command = new UpsertTemplateTranslationCommand(body.LanguageCode, body.Subject, body.Body, body.PushTitle, body.DeepLink);
            var result = await service.UpdateAsync(id, command, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        });
    }
}
