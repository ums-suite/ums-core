using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Documents.Api.Contracts;
using UMS.Modules.Documents.Application.BulkJobs;
using UMS.Modules.Documents.Application.Permissions;
using UMS.Modules.Documents.Domain.Common;
using UMS.Shared.Authorization;
using UMS.Shared.ErrorHandling;

namespace UMS.Modules.Documents.Api.Endpoints;

/// <summary>DOC-5/DOC-7: the async/bulk generation path's own endpoints (requirement-spec.md documents §2/§6).</summary>
internal static class BulkJobEndpoints
{
    public static void MapBulkJobEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/generate-bulk", async (RequestBulkGenerationRequestBody body, HttpContext httpContext, RequestBulkGenerationService service, CancellationToken cancellationToken) =>
        {
            if (!Enum.TryParse<DocumentType>(body.DocumentType, ignoreCase: true, out var documentType))
            {
                return Results.BadRequest(new { code = "document.type_invalid", message = $"'{body.DocumentType}' is not a recognized DocumentType." });
            }

            var command = new RequestBulkGenerationCommand(
                documentType,
                httpContext.User.GetUserId(),
                body.Items.Select(i => new RequestBulkGenerationItemInput(i.OwnerId, i.SourceReferenceId, i.Fields)).ToList());

            var result = await service.RequestAsync(command, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(dto => Results.Accepted($"/api/v1/documents/jobs/{dto.Id}", dto), error => error.ToProblemResult(httpContext));
        }).RequirePermission(DocumentPermissions.GenerateBulk);

        group.MapGet("/jobs/{id:guid}", async (Guid id, HttpContext httpContext, BulkJobStatusService service, CancellationToken cancellationToken) =>
        {
            var result = await service.GetAsync(id, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(DocumentPermissions.GenerateBulk);
    }
}
