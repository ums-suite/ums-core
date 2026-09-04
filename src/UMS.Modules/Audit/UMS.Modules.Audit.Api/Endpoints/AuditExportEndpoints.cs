using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Audit.Api.Contracts;
using UMS.Modules.Audit.Application.Entries;
using UMS.Modules.Audit.Application.Exports;
using UMS.Modules.Audit.Application.Permissions;
using UMS.Modules.Audit.Domain.Exports;
using UMS.Shared.Authorization;
using UMS.Shared.ErrorHandling;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Audit.Api.Endpoints;

/// <summary>AUD-9/AUD-10: async filtered export request/status (requirement-spec.md audit §6, ADR-0014).</summary>
internal static class AuditExportEndpoints
{
    public static void MapAuditExportEndpoints(this RouteGroupBuilder group)
    {
        var exports = group.MapGroup("/exports").RequirePermission(AuditPermissions.ExportGenerate);

        exports.MapPost("/", async (RequestExportRequestBody body, HttpContext httpContext, AuditExportService service, CancellationToken cancellationToken) =>
        {
            if (!Enum.TryParse<ExportFormat>(body.Format, ignoreCase: true, out var format))
            {
                return Error.Validation("audit_export.invalid_format", "Format must be 'Csv' or 'Pdf'.").ToProblemResult(httpContext);
            }

            var filter = new AuditEntryFilter(body.EntityType, body.EntityId, body.ActorId, body.DateFrom, body.DateTo, body.Action, body.Application);
            var dto = await service.RequestExportAsync(httpContext.User.GetUserId(), new RequestExportCommand(filter, format), cancellationToken).ConfigureAwait(false);
            return Results.Created($"/api/v1/audit/exports/{dto.Id}", dto);
        });

        exports.MapGet("/{id:guid}", async (Guid id, HttpContext httpContext, AuditExportService service, CancellationToken cancellationToken) =>
        {
            var result = await service.GetStatusAsync(id, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        });
    }
}
