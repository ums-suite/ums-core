using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Content.Api.Contracts;
using UMS.Modules.Content.Application.Banners;
using UMS.Modules.Content.Application.Common;
using UMS.Modules.Content.Application.Permissions;
using UMS.Shared.Authorization;
using UMS.Shared.ErrorHandling;

namespace UMS.Modules.Content.Api.Endpoints;

/// <summary>CNT-8/CNT-9: requirement-spec.md §6 `/content/banners`.</summary>
internal static class BannerEndpoints
{
    public static void MapBannerEndpoints(this RouteGroupBuilder group)
    {
        var banners = group.MapGroup("/banners");

        banners.MapGet("/", async (BannerService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.ListActiveAsync(cancellationToken).ConfigureAwait(false)));

        banners.MapGet("/{id:guid}", async (Guid id, HttpContext httpContext, BannerService service, CancellationToken cancellationToken) =>
        {
            var result = await service.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(ContentPermissions.BannerWrite);

        banners.MapPost("/", async (CreateBannerRequest request, HttpContext httpContext, BannerService service, CancellationToken cancellationToken) =>
        {
            var result = await service.CreateAsync(request.Headline, request.ImageUrl, request.LinkUrl, request.SortOrder, httpContext.User.GetUserId(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(dto => Results.Created($"/api/v1/content/banners/{dto.Id}", dto), error => error.ToProblemResult(httpContext));
        }).RequirePermission(ContentPermissions.BannerWrite);

        banners.MapPut("/{id:guid}", async (Guid id, UpdateBannerDetailsRequest request, HttpContext httpContext, BannerService service, CancellationToken cancellationToken) =>
        {
            var result = await service.UpdateDetailsAsync(id, request.Headline, request.ImageUrl, request.LinkUrl, request.SortOrder, request.Version, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(ContentPermissions.BannerWrite);

        banners.MapPut("/{id:guid}/schedule", async (Guid id, UpdateBannerScheduleRequest request, HttpContext httpContext, BannerService service, CancellationToken cancellationToken) =>
        {
            var result = await service.UpdateScheduleAsync(id, request.PublishAt, request.ExpireAt, request.Version, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(ContentPermissions.BannerWrite);

        banners.MapPost("/{id:guid}/schedule", async (Guid id, BannerVersionedActionRequest request, HttpContext httpContext, BannerService service, CancellationToken cancellationToken) =>
        {
            var result = await service.ScheduleAsync(id, request.Version, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(ContentPermissions.BannerWrite);

        banners.MapPost("/{id:guid}/publish", async (Guid id, BannerVersionedActionRequest request, HttpContext httpContext, BannerService service, CancellationToken cancellationToken) =>
        {
            var audit = new AuditContext(httpContext.User.GetUserId(), httpContext.Connection.RemoteIpAddress?.ToString(), httpContext.GetCorrelationId());
            var result = await service.PublishAsync(id, audit, request.Version, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(ContentPermissions.BannerPublish);

        banners.MapPost("/{id:guid}/archive", async (Guid id, BannerVersionedActionRequest request, HttpContext httpContext, BannerService service, CancellationToken cancellationToken) =>
        {
            var audit = new AuditContext(httpContext.User.GetUserId(), httpContext.Connection.RemoteIpAddress?.ToString(), httpContext.GetCorrelationId());
            var result = await service.ArchiveAsync(id, audit, request.Version, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(ContentPermissions.BannerPublish);
    }
}
