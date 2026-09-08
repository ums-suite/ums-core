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
    public const string RegulatoryManage = "reporting.regulatory.manage";
    public const string RegulatoryRun = "reporting.regulatory.run";
}
