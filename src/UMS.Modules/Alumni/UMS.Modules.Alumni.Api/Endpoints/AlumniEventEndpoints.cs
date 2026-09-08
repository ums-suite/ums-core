using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Alumni.Application.AlumniEvents;
using UMS.Modules.Alumni.Application.Common;
using UMS.Modules.Alumni.Application.Permissions;
using UMS.Shared.Authorization;
using UMS.Shared.ErrorHandling;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Alumni.Api.Endpoints;

/// <summary>ALM-14: requirement-spec.md §6 <c>/alumni/events/{id}/rsvp</c>. Event CREATION is a minimal extension beyond the ticket's own literal API surface - see <see cref="Domain.AlumniEvents.AlumniEvent"/>'s own remarks.</summary>
internal static class AlumniEventEndpoints
{
    public static void MapAlumniEventEndpoints(this RouteGroupBuilder group)
    {
        var events = group.MapGroup("/events");

        events.MapPost("/", async (CreateAlumniEventRequest body, HttpContext httpContext, AlumniEventService service, CancellationToken cancellationToken) =>
        {
            var result = await service.CreateAsync(body, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(dto => Results.Created($"/api/v1/alumni/events/{dto.Id}", dto), error => error.ToProblemResult(httpContext));
        }).RequirePermission(AlumniPermissions.ChapterManage);

        events.MapGet("/{id:guid}", async (Guid id, HttpContext httpContext, AlumniEventService service, CancellationToken cancellationToken) =>
        {
            var result = await service.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequireLiveSession();

        // requirement-spec.md §2.7: simple RSVP (Going/Interested/NotGoing + optional guest count), no seating/capacity enforcement in v1.
        events.MapPost("/{id:guid}/rsvp", async (Guid id, SubmitRsvpRequest body, HttpContext httpContext, AlumniEventRsvpService service, CallerAlumnusResolver resolver, CancellationToken cancellationToken) =>
        {
            var alumnus = await resolver.ResolveAsync(httpContext.User.GetUserId(), cancellationToken).ConfigureAwait(false);
            if (alumnus is null)
            {
                return Error.Forbidden("rsvp.not_an_alumnus", "Only an Alumnus may RSVP to an alumni event.").ToProblemResult(httpContext);
            }

            var result = await service.SubmitAsync(id, alumnus.Id.Value, body, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequireLiveSession();

        events.MapGet("/{id:guid}/rsvps", async (Guid id, AlumniEventRsvpService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.ListByEventAsync(id, cancellationToken).ConfigureAwait(false))).RequirePermission(AlumniPermissions.ChapterManage);
    }
}
