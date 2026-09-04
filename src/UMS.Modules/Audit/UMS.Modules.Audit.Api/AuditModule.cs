using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Audit.Api.Endpoints;

namespace UMS.Modules.Audit.Api;

/// <summary>Audit's endpoints register themselves under <c>/api/v1/audit/...</c>, mirroring Identity's own <c>IdentityModule</c>. The Host calls this one extension - it never maps an Audit route directly itself.</summary>
public static class AuditModule
{
    public static IEndpointRouteBuilder MapAuditModule(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/audit").WithTags("Audit");

        group.MapAuditEntryEndpoints();
        group.MapAuditExportEndpoints();

        return endpoints;
    }
}
