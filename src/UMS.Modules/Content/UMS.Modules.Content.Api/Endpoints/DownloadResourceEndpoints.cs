using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Content.Api.Contracts;
using UMS.Modules.Content.Application.Downloads;
using UMS.Modules.Content.Application.Permissions;
using UMS.Shared.Authorization;
using UMS.Shared.ErrorHandling;

namespace UMS.Modules.Content.Api.Endpoints;

/// <summary>CNT-11: requirement-spec.md §6 `/content/downloads`.</summary>
internal static class DownloadResourceEndpoints
{
    public static void MapDownloadResourceEndpoints(this RouteGroupBuilder group)
    {
        var downloads = group.MapGroup("/downloads");

        downloads.MapGet("/", async (string? category, int skip, int take, DownloadResourceService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.ListPublishedAsync(category, skip, take, cancellationToken).ConfigureAwait(false)));

        downloads.MapPost("/", async (CreateDownloadResourceRequest request, HttpContext httpContext, DownloadResourceService service, CancellationToken cancellationToken) =>
        {
            var result = await service.CreateAsync(request.Title, request.Category, request.ArtifactId, httpContext.User.GetUserId(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(dto => Results.Created($"/api/v1/content/downloads/{dto.Id}", dto), error => error.ToProblemResult(httpContext));
        }).RequirePermission(ContentPermissions.DownloadWrite);

        downloads.MapPut("/{id:guid}", async (Guid id, UpdateDownloadResourceMetadataRequest request, HttpContext httpContext, DownloadResourceService service, CancellationToken cancellationToken) =>
        {
            var result = await service.UpdateMetadataAsync(id, request.Title, request.Category, request.Version, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(ContentPermissions.DownloadWrite);

        downloads.MapPut("/{id:guid}/schedule", async (Guid id, UpdateDownloadResourceScheduleRequest request, HttpContext httpContext, DownloadResourceService service, CancellationToken cancellationToken) =>
        {
            var result = await service.UpdateScheduleAsync(id, request.PublishAt, request.ExpireAt, request.Version, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(ContentPermissions.DownloadWrite);

        downloads.MapPost("/{id:guid}/schedule", async (Guid id, DownloadResourceVersionedActionRequest request, HttpContext httpContext, DownloadResourceService service, CancellationToken cancellationToken) =>
        {
            var result = await service.ScheduleAsync(id, request.Version, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(ContentPermissions.DownloadWrite);

        downloads.MapPost("/{id:guid}/publish", async (Guid id, DownloadResourceVersionedActionRequest request, HttpContext httpContext, DownloadResourceService service, CancellationToken cancellationToken) =>
        {
            var result = await service.PublishAsync(id, request.Version, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(ContentPermissions.DownloadWrite);

        downloads.MapPost("/{id:guid}/archive", async (Guid id, DownloadResourceVersionedActionRequest request, HttpContext httpContext, DownloadResourceService service, CancellationToken cancellationToken) =>
        {
            var result = await service.ArchiveAsync(id, request.Version, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(ContentPermissions.DownloadWrite);
    }
}
