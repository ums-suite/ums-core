using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Identity.Api.Endpoints;

namespace UMS.Modules.Identity.Api;

/// <summary>
/// Identity's endpoints register themselves under <c>/api/v1/identity/...</c>
/// (src/Host/Program.cs's own comment; release/DEVELOPMENT_PLAN.md Flow #4). The Host calls this
/// one extension - it never maps an Identity route directly itself.
/// </summary>
public static class IdentityModule
{
    public static IEndpointRouteBuilder MapIdentityModule(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/identity").WithTags("Identity");

        group.MapAuthEndpoints();
        group.MapUserEndpoints();
        group.MapRoleEndpoints();
        group.MapSessionEndpoints();
        group.MapPermissionEndpoints();

        return endpoints;
    }
}
