using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Documents.Api.Endpoints;

namespace UMS.Modules.Documents.Api;

/// <summary>Documents' endpoints register themselves under <c>/api/v1/documents/...</c>, mirroring Audit's own <c>AuditModule</c>. The Host calls this one extension - it never maps a Documents route directly itself.</summary>
public static class DocumentsModule
{
    public static IEndpointRouteBuilder MapDocumentsModule(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/documents").WithTags("Documents");

        group.MapTemplateEndpoints();
        group.MapGenerationEndpoints();
        group.MapBulkJobEndpoints();
        group.MapVerificationEndpoints();
        group.MapUploadEndpoints();

        return endpoints;
    }
}
