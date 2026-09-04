using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Documents.Api.Contracts;
using UMS.Modules.Documents.Application.Permissions;
using UMS.Modules.Documents.Application.Uploads;
using UMS.Shared.Authorization;
using UMS.Shared.ErrorHandling;

namespace UMS.Modules.Documents.Api.Endpoints;

/// <summary>requirement-spec.md documents §2 Uploaded Artifact Storage / §6's three upload endpoints.</summary>
internal static class UploadEndpoints
{
    public static void MapUploadEndpoints(this RouteGroupBuilder group)
    {
        var uploads = group.MapGroup("/uploads");

        uploads.MapPost("/", async (RequestUploadRequestBody body, HttpContext httpContext, UploadedArtifactService service, CancellationToken cancellationToken) =>
        {
            var result = await service.RequestUploadAsync(body.OwnerId, body.ArtifactType, body.MimeType, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(dto => Results.Created($"/api/v1/documents/uploads/{dto.Id}", dto), error => error.ToProblemResult(httpContext));
        }).RequirePermission(DocumentPermissions.Generate);

        uploads.MapPost("/{id:guid}/confirm", async (Guid id, HttpContext httpContext, UploadedArtifactService service, CancellationToken cancellationToken) =>
        {
            var result = await service.ConfirmAsync(id, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(DocumentPermissions.Generate);

        uploads.MapGet("/{id:guid}", async (Guid id, HttpContext httpContext, UploadedArtifactService service, IPermissionResolver permissionResolver, CancellationToken cancellationToken) =>
        {
            var userId = httpContext.User.GetUserId();
            var sessionId = httpContext.User.GetSessionId();
            var outcome = await permissionResolver.CheckAsync(userId, sessionId, DocumentPermissions.Read, cancellationToken).ConfigureAwait(false);
            var callerCanReadAny = outcome == PermissionCheckOutcome.Granted;

            var result = await service.GetAsync(id, userId, callerCanReadAny, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequireLiveSession();
    }
}
