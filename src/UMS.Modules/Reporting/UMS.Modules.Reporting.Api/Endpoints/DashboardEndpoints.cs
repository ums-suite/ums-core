using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Reporting.Application.DashboardMetrics;
using UMS.Modules.Reporting.Application.Permissions;
using UMS.Shared.Authorization;

namespace UMS.Modules.Reporting.Api.Endpoints;

/// <summary>
/// RPT-4..9: <c>GET /api/v1/reporting/dashboards/{domain}</c>, one route per source-module
/// dashboard family, each gated by its own <c>reporting.dashboard.&lt;domain&gt;</c> permission
/// (requirement-spec.md §6) - never one shared "any dashboard" permission, so an Admin role can be
/// scoped to, say, only the financial dashboard.
/// </summary>
internal static class DashboardEndpoints
{
    public static void MapDashboardEndpoints(this RouteGroupBuilder group)
    {
        var dashboards = group.MapGroup("/dashboards");

        MapDashboard(dashboards, "academic", AcademicDashboardRefreshService.MetricKeyValue, ReportingPermissions.DashboardReadAcademic);
        MapDashboard(dashboards, "admission", AdmissionDashboardRefreshService.MetricKeyValue, ReportingPermissions.DashboardReadAdmission);
        MapDashboard(dashboards, "financial", FinancialDashboardRefreshService.MetricKeyValue, ReportingPermissions.DashboardReadFinancial);
        MapDashboard(dashboards, "faculty", FacultyDashboardRefreshService.MetricKeyValue, ReportingPermissions.DashboardReadFaculty);
        MapDashboard(dashboards, "hostel", HostelDashboardRefreshService.MetricKeyValue, ReportingPermissions.DashboardReadHostel);
        MapDashboard(dashboards, "library", LibraryDashboardRefreshService.MetricKeyValue, ReportingPermissions.DashboardReadLibrary);

        // Flow #26: the seventh, Content-owned dashboard - a deliberate scope extension, not one of
        // requirement-spec.md §2.2's original six (see UMS.Shared.Content.IContentReportingQuery's
        // own remarks). No "research" route exists alongside it - the "research-dashboard" metric
        // has no Admin-facing GET route of its own (ResearchDashboardRefreshService's own remarks).
        MapDashboard(dashboards, "content", ContentDashboardRefreshService.MetricKeyValue, ReportingPermissions.DashboardReadContent);

        // Flow #31: the eighth/ninth, Alumni-/Career-owned dashboards - the same deliberate scope
        // extension Flow #26 already made for Content, applied identically here (see
        // UMS.Shared.Alumni.IAlumniReportingQuery's/UMS.Shared.Career.ICareerReportingQuery's own
        // remarks). Unlike Flow #26's Research half, neither module had an existing regulatory-report
        // seeder proxy to fix - this is purely additive.
        MapDashboard(dashboards, "alumni", AlumniDashboardRefreshService.MetricKeyValue, ReportingPermissions.DashboardReadAlumni);
        MapDashboard(dashboards, "career", CareerDashboardRefreshService.MetricKeyValue, ReportingPermissions.DashboardReadCareer);
    }

    private static void MapDashboard(RouteGroupBuilder group, string route, string metricKey, string permission)
    {
        group.MapGet($"/{route}", async (DashboardMetricReadService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetAsync(metricKey, cancellationToken).ConfigureAwait(false))).RequirePermission(permission);
    }
}
