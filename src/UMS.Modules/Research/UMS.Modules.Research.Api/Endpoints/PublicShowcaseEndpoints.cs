using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Research.Application.Grants;
using UMS.Modules.Research.Application.InstitutionalRepositoryEntries;
using UMS.Modules.Research.Application.Publications;

namespace UMS.Modules.Research.Api.Endpoints;

/// <summary>
/// RES-13: requirement-spec.md §2 Public Research Showcase, §6 public API rows - a SEPARATE,
/// anonymous, server-side-query-filtered surface requiring NO permission at all (§2 Permission
/// Strings' own closing sentence), HTTP-cacheable (§5 Caching row). Never routed through Content,
/// which does not own this data (§7) - called directly by `ums-public-web`.
/// </summary>
internal static class PublicShowcaseEndpoints
{
    public static void MapPublicShowcaseEndpoints(this RouteGroupBuilder group)
    {
        var showcase = group.MapGroup("/public");

        showcase.MapGet("/publications", async (int? skip, int? take, HttpContext httpContext, PublicationService service, CancellationToken cancellationToken) =>
        {
            var page = await service.ListPubliclyVisibleAsync(skip ?? 0, take ?? 50, cancellationToken).ConfigureAwait(false);
            return httpContext.PublicCacheableJson(page);
        }).AllowAnonymous();

        showcase.MapGet("/grants", async (int? skip, int? take, HttpContext httpContext, GrantService service, CancellationToken cancellationToken) =>
        {
            var page = await service.ListPubliclyVisibleAsync(skip ?? 0, take ?? 50, cancellationToken).ConfigureAwait(false);
            return httpContext.PublicCacheableJson(page);
        }).AllowAnonymous();

        // requirement-spec.md §4/§8: an embargoed entry is excluded from every anonymous/public read
        // regardless of accessLevel - IInstitutionalRepositoryEntryRepository.ListPublicAsync already
        // enforces `!isEmbargoed` server-side (never a client-supplied filter, §2 visibility-filtering
        // decision).
        showcase.MapGet("/repository-entries", async (int? skip, int? take, HttpContext httpContext, InstitutionalRepositoryEntryService service, CancellationToken cancellationToken) =>
        {
            var page = await service.ListPublicAsync(skip ?? 0, take ?? 50, cancellationToken).ConfigureAwait(false);
            return httpContext.PublicCacheableJson(page);
        }).AllowAnonymous();
    }
}
