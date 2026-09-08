using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Reporting.Application.Permissions;
using UMS.Modules.Reporting.Application.RegulatoryReports;
using UMS.Modules.Reporting.Domain.RegulatoryReports;
using UMS.Shared.Authorization;
using UMS.Shared.ErrorHandling;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Reporting.Api.Endpoints;

/// <summary>
/// RPT-12/RPT-13: <c>POST /regulatory-reports/{definitionId}/run</c> always enqueues and returns a
/// run id immediately (requirement-spec.md §4 "Report generation is always async" - never inline
/// generation regardless of size); <c>GET /regulatory-report-runs/{runId}</c> polls the resulting
/// status/result.
/// </summary>
internal static class RegulatoryReportRunEndpoints
{
    public static void MapRegulatoryReportRunEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/regulatory-reports/{definitionId:guid}/run", async (Guid definitionId, RunRegulatoryReportHttpRequest body, HttpContext httpContext, RegulatoryReportRunService service, CancellationToken cancellationToken) =>
        {
            var result = await service.EnqueueAsync(definitionId, body.ParametersJson ?? "{}", body.Format, httpContext.GetAuditContext(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(value => Results.Accepted($"/api/v1/reporting/regulatory-report-runs/{value.RunId}", value), error => error.ToProblemResult(httpContext));
        }).RequirePermission(ReportingPermissions.RegulatoryRun);

        group.MapGet("/regulatory-report-runs/{runId:guid}", async (Guid runId, HttpContext httpContext, RegulatoryReportRunService service, CancellationToken cancellationToken) =>
        {
            var result = await service.GetStatusAsync(runId, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(ReportingPermissions.RegulatoryRun);
    }

    private sealed record RunRegulatoryReportHttpRequest(string? ParametersJson, RegulatoryReportFormat Format);
}
