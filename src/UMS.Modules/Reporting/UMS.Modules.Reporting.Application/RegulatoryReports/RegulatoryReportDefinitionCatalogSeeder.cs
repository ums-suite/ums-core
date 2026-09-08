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
/// <b>Documented gap:</b> RPT-3 scopes this build's new reporting-query contracts to exactly six
/// modules (Academic, Admission, Finance, Faculty, Hostel, Library) - the six that have their own
/// dashboard (RPT-4..9). Several of BRD §13's eleven regulatory categories genuinely need data no
/// dashboard surfaces yet: Gender/International-student distribution needs Student-owned
/// demographic fields, Research needs Faculty's own <c>ResearchProfile</c> data, and a full campus
/// Infrastructure inventory needs Organization's own facilities data - none of which has a
/// reporting-query contract in this base flow (out of RPT-3's explicit scope). Rather than omit
/// those categories entirely, each below draws on the nearest available already-built dashboard
/// field as an honestly-labeled proxy (see each category's own field labels) - a real, working
/// seed catalog today, with the genuine metric each category ultimately needs flagged here for a
/// future "Reporting top-up" flow once Student/Organization/Faculty gain their own reporting-query
/// contracts.
/// </para>
/// </summary>
public static class RegulatoryReportDefinitionCatalogSeeder
{
    public static IReadOnlyList<RegulatoryReportDefinitionSeed> Catalog { get; } =
    [
        new("Student Enrollment", RegulatoryReportCategory.StudentEnrollment, ["academic-dashboard"],
            [("TotalEnrollments", "Total Enrollments"), ("ActiveEnrollments", "Active Enrollments"), ("NewEnrollmentsThisSession", "New Enrollments (Current Session)"), ("GraduatingStudents", "Graduating Students")]),

        // Documented proxy: true gender distribution needs Student's own demographic fields (no
        // reporting-query contract exists for Student in this base flow) - approximated with the
        // aggregate enrollment count until that contract exists.
        new("Gender Distribution", RegulatoryReportCategory.GenderDistribution, ["academic-dashboard"],
            [("TotalEnrollments", "Total Enrollments (gender breakdown pending Student reporting contract)")]),

        new("Program Statistics", RegulatoryReportCategory.ProgramStatistics, ["academic-dashboard"],
            [("TotalEnrollments", "Total Enrollments"), ("CourseOfferingPerformance", "Course/Department Performance")]),

        new("Faculty/Staff Statistics", RegulatoryReportCategory.FacultyStaffStatistics, ["faculty-dashboard"],
            [("TotalFacultyMembers", "Total Faculty Members"), ("FacultyByStatus", "Faculty By Status")]),

        new("Graduation", RegulatoryReportCategory.Graduation, ["academic-dashboard"],
            [("GraduatingStudents", "Graduating Students"), ("PassRate", "Pass Rate")]),

        new("Academic Performance", RegulatoryReportCategory.AcademicPerformance, ["academic-dashboard"],
            [("GradeDistribution", "Grade Distribution"), ("PassRate", "Pass Rate"), ("DropoutRate", "Dropout Rate")]),

        // Documented proxy: Faculty's own ResearchProfile output data has no reporting-query
        // contract in this base flow - approximated with faculty headcount pending that contract.
        new("Research", RegulatoryReportCategory.Research, ["faculty-dashboard"],
            [("TotalFacultyMembers", "Total Faculty Members (research-output detail pending Faculty ResearchProfile reporting contract)")]),

        new("Financial Information", RegulatoryReportCategory.FinancialInformation, ["financial-dashboard"],
            [("TotalCollection", "Total Collection"), ("OutstandingFees", "Outstanding Fees"), ("TotalRefunds", "Total Refunds"), ("RevenueByCategory", "Revenue By Category")]),

        // Documented proxy: a full campus facilities inventory needs Organization's own data (no
        // reporting-query contract in this base flow) - approximated with Hostel bed capacity.
        new("Infrastructure", RegulatoryReportCategory.Infrastructure, ["hostel-dashboard"],
            [("TotalBeds", "Total Hostel Beds"), ("OccupiedBeds", "Occupied Beds"), ("OccupancyPercentage", "Occupancy Percentage (campus-wide facilities inventory pending Organization reporting contract)")]),

        // Documented proxy: no distinct Scholarship concept is exposed by Finance's reporting
        // query in this base flow - approximated with the refund aggregate.
        new("Scholarships", RegulatoryReportCategory.Scholarships, ["financial-dashboard"],
            [("TotalRefunds", "Total Refunds/Waivers (dedicated Scholarship tracking pending a future Finance reporting-contract extension)")]),

        // Documented proxy: nationality/international-student status is a Student-owned field with
        // no reporting-query contract in this base flow - approximated with total enrollment.
        new("International Students", RegulatoryReportCategory.InternationalStudents, ["academic-dashboard"],
            [("TotalEnrollments", "Total Enrollments (international-student breakdown pending Student reporting contract)")]),
    ];
}

public sealed record RegulatoryReportDefinitionSeed(string Name, RegulatoryReportCategory Category, IReadOnlyList<string> SourceQueryReferences, IReadOnlyList<(string FieldKey, string Label)> Fields)
{
    public string SourceQueryReferencesJson => JsonSerializer.Serialize(SourceQueryReferences);
}
