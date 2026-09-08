using UMS.Modules.Reporting.Application.Permissions;
using UMS.Shared.Authorization;

namespace UMS.Modules.Reporting.Infrastructure.Authorization;

internal sealed class ReportingPermissionManifest : IPermissionManifest
{
    public string OwningModule => "reporting";

    public IReadOnlyCollection<PermissionDefinition> Permissions { get; } =
    [
        new(ReportingPermissions.DashboardReadAcademic, "View the Academic dashboard (Admin)."),
        new(ReportingPermissions.DashboardReadAdmission, "View the Admission dashboard (Admin)."),
        new(ReportingPermissions.DashboardReadFinancial, "View the Financial dashboard (Admin)."),
        new(ReportingPermissions.DashboardReadFaculty, "View the Faculty dashboard (Admin)."),
        new(ReportingPermissions.DashboardReadHostel, "View the Hostel dashboard (Admin)."),
        new(ReportingPermissions.DashboardReadLibrary, "View the Library dashboard (Admin)."),
        new(ReportingPermissions.DashboardReadContent, "View the Content dashboard (Admin)."),
        new(ReportingPermissions.RegulatoryManage, "Create/edit RegulatoryReportDefinitions (Admin)."),
        new(ReportingPermissions.RegulatoryRun, "Enqueue a RegulatoryReportRun and view its status (Admin)."),
    ];
}
