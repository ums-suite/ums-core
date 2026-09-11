namespace UMS.Modules.Reporting.Application.Permissions;

/// <summary>
/// requirement-spec.md §6: "every endpoint requires an Admin-facing permission
/// (<c>reporting.dashboard.read.&lt;domain&gt;</c>, <c>reporting.regulatory.manage</c>, ADR-0006)".
/// Every string is 3+ dot-separated lowercase-alphanumeric segments, no underscores (this repo's
/// established permission-string shape).
/// </summary>
public static class ReportingPermissions
{
    public const string DashboardReadAcademic = "reporting.dashboard.academic";
    public const string DashboardReadAdmission = "reporting.dashboard.admission";
    public const string DashboardReadFinancial = "reporting.dashboard.financial";
    public const string DashboardReadFaculty = "reporting.dashboard.faculty";
    public const string DashboardReadHostel = "reporting.dashboard.hostel";
    public const string DashboardReadLibrary = "reporting.dashboard.library";

    /// <summary>Flow #26: the seventh, Content-owned admin dashboard - see <c>UMS.Shared.Content.IContentReportingQuery</c>'s own remarks for why this is a deliberate scope extension. No separate permission exists for the "research-dashboard" DashboardMetric - it has no Admin-facing GET route of its own (see <c>ResearchDashboardRefreshService</c>'s own remarks), so <see cref="RegulatoryRun"/> already gates its only consumer.</summary>
    public const string DashboardReadContent = "reporting.dashboard.content";

    /// <summary>Flow #31: the eighth, Alumni-owned admin dashboard - see <c>UMS.Shared.Alumni.IAlumniReportingQuery</c>'s own remarks for why this is a deliberate scope extension, the same judgment call Flow #26 already made for Content.</summary>
    public const string DashboardReadAlumni = "reporting.dashboard.alumni";

    /// <summary>Flow #31: the ninth, Career-owned admin dashboard - see <c>UMS.Shared.Career.ICareerReportingQuery</c>'s own remarks for why this is a deliberate scope extension, the same judgment call Flow #26 already made for Content.</summary>
    public const string DashboardReadCareer = "reporting.dashboard.career";

    public const string RegulatoryManage = "reporting.regulatory.manage";
    public const string RegulatoryRun = "reporting.regulatory.run";
}
