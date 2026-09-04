using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Documents.Api.Contracts;
using UMS.Modules.Documents.Application.Permissions;
using UMS.Modules.Documents.Application.Templates;
using UMS.Modules.Documents.Domain.Common;
using UMS.Shared.Authorization;
using UMS.Shared.ErrorHandling;

namespace UMS.Modules.Documents.Api.Endpoints;

/// <summary>DOC-1: Admin-managed template publish/read (requirement-spec.md documents §2/§6).</summary>
internal static class TemplateEndpoints
{
    public static void MapTemplateEndpoints(this RouteGroupBuilder group)
    {
        var templates = group.MapGroup("/templates").RequirePermission(DocumentPermissions.TemplateManage);

        templates.MapPost("/", async (PublishTemplateRequestBody body, HttpContext httpContext, DocumentTemplateService service, CancellationToken cancellationToken) =>
        {
            if (!Enum.TryParse<DocumentType>(body.DocumentType, ignoreCase: true, out var documentType))
            {
                return Results.BadRequest(new { code = "document_template.type_invalid", message = $"'{body.DocumentType}' is not a recognized DocumentType." });
            }

            var command = new PublishTemplateCommand(
                documentType,
                body.LayoutAssetKey,
                body.Translations.Select(t => new PublishTemplateTranslationInput(t.Language, t.Title, t.LabelsJson)).ToList());

            var result = await service.PublishAsync(command, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(dto => Results.Created($"/api/v1/documents/templates/{dto.Id}", dto), error => error.ToProblemResult(httpContext));
        });

        templates.MapGet("/", async (string? documentType, HttpContext httpContext, DocumentTemplateService service, CancellationToken cancellationToken) =>
        {
            DocumentType? parsed = null;
            if (documentType is not null)
            {
                if (!Enum.TryParse<DocumentType>(documentType, ignoreCase: true, out var value))
                {
                    return Results.BadRequest(new { code = "document_template.type_invalid", message = $"'{documentType}' is not a recognized DocumentType." });
                }

                parsed = value;
            }

            var list = await service.ListAsync(parsed, cancellationToken).ConfigureAwait(false);
            return Results.Ok(list);
        });

        templates.MapGet("/current", async (string documentType, HttpContext httpContext, DocumentTemplateService service, CancellationToken cancellationToken) =>
        {
            if (!Enum.TryParse<DocumentType>(documentType, ignoreCase: true, out var parsed))
            {
                return Results.BadRequest(new { code = "document_template.type_invalid", message = $"'{documentType}' is not a recognized DocumentType." });
            }

            var result = await service.GetCurrentAsync(parsed, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        });
    }
}
