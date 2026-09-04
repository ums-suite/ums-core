using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Organization.Application.Hierarchy;
using UMS.Shared.ErrorHandling;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Organization.Api.Endpoints;

/// <summary>
/// ORG-9/ORG-10: `GET /organization-tree` (full or subtree via `?rootId=`) and
/// `GET /nodes/{id}/ancestors` (requirement-spec.md organization §2/§6) - cache-friendly, used by
/// admin tree pickers and Identity's ScopeGrant display/breadcrumbs. Both `AllowAnonymous` per
/// <see cref="Application.Permissions.OrganizationPermissions"/>'s own remarks (§5's own
/// CDN-cacheable public-catalog framing).
/// </summary>
internal static class HierarchyEndpoints
{
    public static void MapHierarchyEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/organization-tree", async (Guid? rootId, string? lang, HttpContext httpContext, HierarchyQueryService service, CancellationToken cancellationToken) =>
        {
            var result = await service.GetTreeAsync(rootId, lang, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).AllowAnonymous();

        group.MapGet("/nodes/{id:guid}/ancestors", async (Guid id, string? lang, HttpContext httpContext, HierarchyQueryService service, CancellationToken cancellationToken) =>
        {
            var result = await service.GetAncestorsAsync(id, lang, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).AllowAnonymous();
    }
}
