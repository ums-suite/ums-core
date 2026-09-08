using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Alumni.Application.Chapters;
using UMS.Modules.Alumni.Application.Common;
using UMS.Modules.Alumni.Application.Permissions;
using UMS.Shared.Authorization;
using UMS.Shared.ErrorHandling;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Alumni.Api.Endpoints;

/// <summary>ALM-4: requirement-spec.md §6 <c>/alumni/chapters</c>, <c>/chapters/{id}/join</c>.</summary>
internal static class ChapterEndpoints
{
    public static void MapChapterEndpoints(this RouteGroupBuilder group)
    {
        var chapters = group.MapGroup("/chapters");

        chapters.MapGet("/", async (int? skip, int? take, ChapterService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.ListAsync(skip ?? 0, take ?? 50, cancellationToken).ConfigureAwait(false))).RequireLiveSession();

        chapters.MapGet("/{id:guid}", async (Guid id, HttpContext httpContext, ChapterService service, CancellationToken cancellationToken) =>
        {
            var result = await service.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequireLiveSession();

        chapters.MapPost("/", async (CreateChapterRequest body, HttpContext httpContext, ChapterService service, CancellationToken cancellationToken) =>
        {
            var result = await service.CreateAsync(body, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(dto => Results.Created($"/api/v1/alumni/chapters/{dto.Id}", dto), error => error.ToProblemResult(httpContext));
        }).RequirePermission(AlumniPermissions.ChapterManage);

        chapters.MapPost("/{id:guid}/join", async (Guid id, HttpContext httpContext, ChapterService service, CallerAlumnusResolver resolver, CancellationToken cancellationToken) =>
        {
            var alumnus = await resolver.ResolveAsync(httpContext.User.GetUserId(), cancellationToken).ConfigureAwait(false);
            if (alumnus is null)
            {
                return Error.Forbidden("chapter.not_an_alumnus", "Only an Alumnus may join a chapter.").ToProblemResult(httpContext);
            }

            var result = await service.JoinAsync(id, alumnus.Id.Value, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequireLiveSession();

        chapters.MapPost("/{id:guid}/leave", async (Guid id, HttpContext httpContext, ChapterService service, CallerAlumnusResolver resolver, CancellationToken cancellationToken) =>
        {
            var alumnus = await resolver.ResolveAsync(httpContext.User.GetUserId(), cancellationToken).ConfigureAwait(false);
            if (alumnus is null)
            {
                return Error.Forbidden("chapter.not_an_alumnus", "Only an Alumnus may leave a chapter.").ToProblemResult(httpContext);
            }

            var result = await service.LeaveAsync(id, alumnus.Id.Value, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequireLiveSession();
    }
}
