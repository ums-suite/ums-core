using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Content.Api.Endpoints;

namespace UMS.Modules.Content.Api;

/// <summary>Content's endpoints register themselves under <c>/api/v1/content/...</c> (requirement-spec.md §6), mirroring every other module's own registration exactly.</summary>
public static class ContentModule
{
    public static IEndpointRouteBuilder MapContentModule(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup("/api/v1/content").WithTags("Content");

        group.MapNoticeEndpoints();
        group.MapEventEndpoints();
        group.MapBannerEndpoints();
        group.MapHomepageSectionEndpoints();
        group.MapDownloadResourceEndpoints();

        return endpoints;
    }
}
