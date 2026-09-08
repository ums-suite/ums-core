using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Content.Api.Contracts;
using UMS.Modules.Content.Application.Events;
using UMS.Modules.Content.Application.Permissions;
using UMS.Shared.Authorization;
using UMS.Shared.ErrorHandling;

namespace UMS.Modules.Content.Api.Endpoints;

/// <summary>CNT-7: requirement-spec.md §6 `/content/events`.</summary>
internal static class EventEndpoints
{
    public static void MapEventEndpoints(this RouteGroupBuilder group)
    {
        var events = group.MapGroup("/events");

        events.MapGet("/", async (DateTimeOffset? from, DateTimeOffset? to, string? audience, Guid? organizationNodeId, int skip, int take, HttpContext httpContext, EventService service, CancellationToken cancellationToken) =>
        {
            var parsedAudience = AudienceParser.ParseSingle(audience);
            var page = await service.ListAsync(from, to, parsedAudience, organizationNodeId, httpContext.GetOptionalUserId(), httpContext.GetPreferredLanguage(), skip, take, cancellationToken).ConfigureAwait(false);
            return Results.Ok(page);
        });

        events.MapGet("/{id:guid}", async (Guid id, HttpContext httpContext, EventService service, CancellationToken cancellationToken) =>
        {
            var result = await service.GetByIdAsync(id, httpContext.GetPreferredLanguage(), httpContext.GetOptionalUserId(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        });

        events.MapPost("/", async (CreateEventRequest request, HttpContext httpContext, EventService service, CancellationToken cancellationToken) =>
        {
            var audience = AudienceParser.Parse(request.Audience);
            if (audience.IsFailure)
            {
                return audience.Error!.ToProblemResult(httpContext);
            }

            var result = await service.CreateAsync(request.Title, request.Body, request.LocationLabel, audience.Value, request.OrganizationNodeId, request.StartAt, request.EndAt, httpContext.User.GetUserId(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(dto => Results.Created($"/api/v1/content/events/{dto.Id}", dto), error => error.ToProblemResult(httpContext));
        }).RequirePermission(ContentPermissions.EventWrite);

        events.MapPut("/{id:guid}", async (Guid id, UpdateEventContentRequest request, HttpContext httpContext, EventService service, CancellationToken cancellationToken) =>
        {
            var result = await service.UpdateContentAsync(id, request.Title, request.Body, request.LocationLabel, request.StartAt, request.EndAt, httpContext.User.GetUserId(), request.Version, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(ContentPermissions.EventWrite);

        events.MapPost("/{id:guid}/translations", async (Guid id, UpsertEventTranslationRequest request, HttpContext httpContext, EventService service, CancellationToken cancellationToken) =>
        {
            var result = await service.UpsertTranslationAsync(id, request.LanguageCode, request.Title, request.Body, request.LocationLabel, httpContext.User.GetUserId(), request.Version, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(ContentPermissions.EventWrite);
    }
}
