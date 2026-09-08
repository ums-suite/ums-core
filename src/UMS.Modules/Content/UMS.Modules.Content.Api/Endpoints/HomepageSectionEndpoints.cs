using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Content.Api.Contracts;
using UMS.Modules.Content.Application.HomepageSections;
using UMS.Modules.Content.Application.Permissions;
using UMS.Shared.Authorization;
using UMS.Shared.ErrorHandling;

namespace UMS.Modules.Content.Api.Endpoints;

/// <summary>CNT-10: requirement-spec.md §6 `/content/homepage-sections` (`GET`/`PUT`).</summary>
internal static class HomepageSectionEndpoints
{
    public static void MapHomepageSectionEndpoints(this RouteGroupBuilder group)
    {
        var sections = group.MapGroup("/homepage-sections");

        sections.MapGet("/", async (HomepageSectionService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.ListEnabledAsync(cancellationToken).ConfigureAwait(false)));

        sections.MapGet("/all", async (HomepageSectionService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.ListAllAsync(cancellationToken).ConfigureAwait(false)))
            .RequirePermission(ContentPermissions.HomepageWrite);

        sections.MapPost("/", async (CreateHomepageSectionRequest request, HttpContext httpContext, HomepageSectionService service, CancellationToken cancellationToken) =>
        {
            var result = await service.CreateAsync(request.SectionKey, request.Title, request.SortOrder, request.IsEnabled, request.ReferenceOrganizationNodeId, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(dto => Results.Created($"/api/v1/content/homepage-sections/{dto.Id}", dto), error => error.ToProblemResult(httpContext));
        }).RequirePermission(ContentPermissions.HomepageWrite);

        sections.MapPut("/{id:guid}", async (Guid id, UpdateHomepageSectionRequest request, HttpContext httpContext, HomepageSectionService service, CancellationToken cancellationToken) =>
        {
            var result = await service.UpdateAsync(id, request.Title, request.SortOrder, request.IsEnabled, request.ReferenceOrganizationNodeId, request.Version, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(ContentPermissions.HomepageWrite);
    }
}
