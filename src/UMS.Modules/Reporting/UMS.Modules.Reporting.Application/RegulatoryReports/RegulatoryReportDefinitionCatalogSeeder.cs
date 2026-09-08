using System.Text.Json;
using UMS.Modules.Reporting.Domain.RegulatoryReports;

namespace UMS.Modules.Reporting.Application.RegulatoryReports;

/// <summary>
/// RPT-14: "Seed the initial RegulatoryReportDefinition catalog" - requirement-spec.md §2.3's
/// starting catalog (BRD §13/archive/srs1.md §33). Applied idempotently (by name) once at startup
/// by <c>Infrastructure.DependencyInjection.UseReportingModuleAsync</c>, the same
/// migrate-then-seed shape every other module's own startup hook already uses.
///
/// <para>
/// <b>Documented gap (four categories remain proxied):</b> RPT-3 scoped the Flow #22 base build's
/// new reporting-query contracts to exactly six modules (Academic, Admission, Finance, Faculty,
/// Hostel, Library) - the six that have their own dashboard (RPT-4..9). Four of BRD §13's eleven
/// regulatory categories still genuinely need data no dashboard surfaces: Gender/International-
/// student distribution needs Student-owned demographic fields, a full campus Infrastructure
/// inventory needs Organization's own facilities data, and Scholarships needs a dedicated Finance
/// concept - none of which has a reporting-query contract yet. Each below still draws on the
/// nearest available already-built dashboard field as an honestly-labeled proxy (see each
/// category's own field labels) - a real, working seed catalog today, with the genuine metric each
/// category ultimately needs flagged here for a future top-up flow once Student/Organization gain
/// their own reporting-query contracts (deliberately NOT this flow - Flow #26 is scoped to
/// Content/Research only, see release/DEVELOPMENT_PLAN.md).
/// </para>
///
/// <para>
/// <b>Fixed by Flow #26 ("Reporting - Content &amp; Research top-up"):</b> the "Research" category
/// below previously proxied Faculty's own headcount pending a Research reporting-query contract
/// (Research, Flow #25, did not exist yet at RPT-3's build time). It now draws on the real
/// <c>UMS.Shared.Research.IResearchReportingQuery</c>-backed <c>"research-dashboard"</c>
/// DashboardMetric instead - genuine Grant/Publication/InstitutionalRepositoryEntry aggregates, no
/// longer a proxy.
/// </para>
/// </summary>
public static class RegulatoryReportDefinitionCatalogSeeder
{
    public static IReadOnlyList<RegulatoryReportDefinitionSeed> Catalog { get; } =
    [
        new(
            "Student Enrollment",
            RegulatoryReportCategory.StudentEnrollment,
            ["academic-dashboard"],
            [("TotalEnrollments", "Total Enrollments"), ("ActiveEnrollments", "Active Enrollments"), ("NewEnrollmentsThisSession", "New Enrollments (Current Session)"), ("GraduatingStudents", "Graduating Students")]),

        // Documented proxy: true gender distribution needs Student's own demographic fields (no
        // reporting-query contract exists for Student in this base flow) - approximated with the
        // aggregate enrollment count until that contract exists.
        new(
            "Gender Distribution",
            RegulatoryReportCategory.GenderDistribution,
            ["academic-dashboard"],
            [("TotalEnrollments", "Total Enrollments (gender breakdown pending Student reporting contract)")]),

        new(
            "Program Statistics",
            RegulatoryReportCategory.ProgramStatistics,
            ["academic-dashboard"],
            [("TotalEnrollments", "Total Enrollments"), ("CourseOfferingPerformance", "Course/Department Performance")]),

        new(
            "Faculty/Staff Statistics",
            RegulatoryReportCategory.FacultyStaffStatistics,
            ["faculty-dashboard"],
            [("TotalFacultyMembers", "Total Faculty Members"), ("FacultyByStatus", "Faculty By Status")]),

        new(
            "Graduation",
            RegulatoryReportCategory.Graduation,
            ["academic-dashboard"],
            [("GraduatingStudents", "Graduating Students"), ("PassRate", "Pass Rate")]),

        new(
            "Academic Performance",
            RegulatoryReportCategory.AcademicPerformance,
            ["academic-dashboard"],
            [("GradeDistribution", "Grade Distribution"), ("PassRate", "Pass Rate"), ("DropoutRate", "Dropout Rate")]),

        // Flow #26: previously proxied with Faculty headcount pending a Research reporting-query
        // contract (Research, Flow #25, did not exist at RPT-3's build time) - now sourced from the
        // real UMS.Shared.Research.IResearchReportingQuery-backed "research-dashboard" DashboardMetric.
        new(
            "Research",
            RegulatoryReportCategory.Research,
            ["research-dashboard"],
            [
                ("TotalActiveGrants", "Total Active Grants"),
                ("ConfirmedFundingAmountByCurrency", "Total Confirmed Funding Amount (by currency)"),
                ("TotalPublications", "Total Publications"),
                ("TotalRepositoryEntries", "Total Institutional Repository Entries"),
            ]),

        new(
            "Financial Information",
            RegulatoryReportCategory.FinancialInformation,
            ["financial-dashboard"],
            [("TotalCollection", "Total Collection"), ("OutstandingFees", "Outstanding Fees"), ("TotalRefunds", "Total Refunds"), ("RevenueByCategory", "Revenue By Category")]),

        // Documented proxy: a full campus facilities inventory needs Organization's own data (no
        // reporting-query contract in this base flow) - approximated with Hostel bed capacity.
        new(
            "Infrastructure",
            RegulatoryReportCategory.Infrastructure,
            ["hostel-dashboard"],
            [("TotalBeds", "Total Hostel Beds"), ("OccupiedBeds", "Occupied Beds"), ("OccupancyPercentage", "Occupancy Percentage (campus-wide facilities inventory pending Organization reporting contract)")]),

        // Documented proxy: no distinct Scholarship concept is exposed by Finance's reporting
        // query in this base flow - approximated with the refund aggregate.
        new(
            "Scholarships",
            RegulatoryReportCategory.Scholarships,
            ["financial-dashboard"],
            [("TotalRefunds", "Total Refunds/Waivers (dedicated Scholarship tracking pending a future Finance reporting-contract extension)")]),

        // Documented proxy: nationality/international-student status is a Student-owned field with
        // no reporting-query contract in this base flow - approximated with total enrollment.
        new(
            "International Students",
            RegulatoryReportCategory.InternationalStudents,
            ["academic-dashboard"],
            [("TotalEnrollments", "Total Enrollments (international-student breakdown pending Student reporting contract)")]),
    ];
}

public sealed record RegulatoryReportDefinitionSeed(string Name, RegulatoryReportCategory Category, IReadOnlyList<string> SourceQueryReferences, IReadOnlyList<(string FieldKey, string Label)> Fields)
{
    public string SourceQueryReferencesJson => JsonSerializer.Serialize(SourceQueryReferences);
}
