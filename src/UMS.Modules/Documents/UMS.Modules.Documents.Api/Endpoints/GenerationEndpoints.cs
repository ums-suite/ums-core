using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Documents.Api.Contracts;
using UMS.Modules.Documents.Application.Generation;
using UMS.Modules.Documents.Application.Permissions;
using UMS.Modules.Documents.Domain.Common;
using UMS.Shared.Authorization;
using UMS.Shared.ErrorHandling;
using UMS.Shared.Observability.Correlation;

namespace UMS.Modules.Documents.Api.Endpoints;

/// <summary>DOC-3/DOC-10/DOC-11/DOC-12: the synchronous single-document path plus read/list/revoke (requirement-spec.md documents §2/§6).</summary>
internal static class GenerationEndpoints
{
    public static void MapGenerationEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/generate", async (GenerateDocumentRequestBody body, HttpContext httpContext, GenerateDocumentService service, CancellationToken cancellationToken) =>
        {
            if (!Enum.TryParse<DocumentType>(body.DocumentType, ignoreCase: true, out var documentType))
            {
                return Results.BadRequest(new { code = "document.type_invalid", message = $"'{body.DocumentType}' is not a recognized DocumentType." });
            }

            var language = LanguageCodeExtensions.TryParse(body.Language);
            var correlationId = CorrelationIdContext.GetOrCreate(httpContext);

            var command = new GenerateDocumentCommand(
                body.OwnerId,
                documentType,
                body.SourceReferenceId,
                body.Fields,
                language,
                httpContext.User.GetUserId(),
                correlationId);

            var result = await service.GenerateAsync(command, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(
                dto => dto.Status == "Ready" ? Results.Ok(dto) : Results.Accepted(value: dto),
                error => error.ToProblemResult(httpContext));
        }).RequirePermission(DocumentPermissions.Generate);

        group.MapGet("/{id:guid}", async (Guid id, HttpContext httpContext, GetDocumentService service, IPermissionResolver permissionResolver, CancellationToken cancellationToken) =>
        {
            var callerCanReadAny = await HasReadAnyPermissionAsync(httpContext, permissionResolver, cancellationToken).ConfigureAwait(false);
            var result = await service.GetAsync(id, httpContext.User.GetUserId(), callerCanReadAny, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequireLiveSession();

        group.MapGet("/", async (Guid? ownerId, string? type, HttpContext httpContext, ListDocumentsService service, IPermissionResolver permissionResolver, CancellationToken cancellationToken) =>
        {
            DocumentType? documentType = null;
            if (type is not null)
            {
                if (!Enum.TryParse<DocumentType>(type, ignoreCase: true, out var parsed))
                {
                    return Results.BadRequest(new { code = "document.type_invalid", message = $"'{type}' is not a recognized DocumentType." });
                }

                documentType = parsed;
            }

            var callerCanReadAny = await HasReadAnyPermissionAsync(httpContext, permissionResolver, cancellationToken).ConfigureAwait(false);
            var result = await service.ListAsync(ownerId, documentType, httpContext.User.GetUserId(), callerCanReadAny, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequireLiveSession();

        group.MapPost("/{id:guid}/revoke", async (Guid id, RevokeDocumentRequestBody body, HttpContext httpContext, RevokeDocumentService service, CancellationToken cancellationToken) =>
        {
            var correlationId = CorrelationIdContext.GetOrCreate(httpContext);
            var actorIpAddress = httpContext.Connection.RemoteIpAddress?.ToString();
            var result = await service.RevokeAsync(id, body.Reason, httpContext.User.GetUserId(), actorIpAddress, correlationId, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(DocumentPermissions.Revoke);
    }

    /// <summary>requirement-spec.md documents §6: "ownership/scope-checked" - the owner may always read their own; <c>document.read</c> grants reading beyond that. Checked live rather than via a static claim, since Permission-Role assignment can change mid-session (identity §4).</summary>
    private static async Task<bool> HasReadAnyPermissionAsync(HttpContext httpContext, IPermissionResolver permissionResolver, CancellationToken cancellationToken)
    {
        var userId = httpContext.User.GetUserId();
        var sessionId = httpContext.User.GetSessionId();
        var outcome = await permissionResolver.CheckAsync(userId, sessionId, DocumentPermissions.Read, cancellationToken).ConfigureAwait(false);
        return outcome == PermissionCheckOutcome.Granted;
    }
}
