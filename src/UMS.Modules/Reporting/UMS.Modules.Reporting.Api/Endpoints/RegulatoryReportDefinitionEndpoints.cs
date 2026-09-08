using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Reporting.Application.Permissions;
using UMS.Modules.Reporting.Application.RegulatoryReports;
using UMS.Shared.Authorization;
using UMS.Shared.ErrorHandling;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Reporting.Api.Endpoints;

/// <summary>RPT-11: <c>GET/POST /regulatory-reports/definitions</c>, <c>GET/PUT /definitions/{id}</c> - every write gated by <c>reporting.regulatory.manage</c> (requirement-spec.md §6), reads by the same permission (definitions are an Admin-facing catalog, not end-user content).</summary>
internal static class RegulatoryReportDefinitionEndpoints
{
    public static void MapRegulatoryReportDefinitionEndpoints(this RouteGroupBuilder group)
    {
        var definitions = group.MapGroup("/regulatory-reports/definitions");

        definitions.MapGet("/", async (int? page, int? pageSize, RegulatoryReportDefinitionService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetPageAsync(page ?? 1, pageSize ?? 20, cancellationToken).ConfigureAwait(false)))
            .RequirePermission(ReportingPermissions.RegulatoryManage);

        definitions.MapGet("/{id:guid}", async (Guid id, HttpContext httpContext, RegulatoryReportDefinitionService service, CancellationToken cancellationToken) =>
        {
            var result = await service.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(ReportingPermissions.RegulatoryManage);

        definitions.MapPost("/", async (CreateRegulatoryReportDefinitionRequest body, HttpContext httpContext, RegulatoryReportDefinitionService service, CancellationToken cancellationToken) =>
        {
            var result = await service.CreateAsync(body, httpContext.GetAuditContext(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(ReportingPermissions.RegulatoryManage);

        definitions.MapPut("/{id:guid}", async (Guid id, UpdateRegulatoryReportDefinitionRequest body, HttpContext httpContext, RegulatoryReportDefinitionService service, CancellationToken cancellationToken) =>
        {
            var result = await service.UpdateAsync(id, body, httpContext.GetAuditContext(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(ReportingPermissions.RegulatoryManage);
    }
}
