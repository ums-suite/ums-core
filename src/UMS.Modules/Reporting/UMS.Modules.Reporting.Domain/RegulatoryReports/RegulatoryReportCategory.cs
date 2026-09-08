namespace UMS.Modules.Reporting.Domain.RegulatoryReports;

/// <summary>RPT-14: the starting catalog reporting requirement-spec.md §2.3/BRD §13 names ("archive/srs1.md §33's report categories are the starting catalog of RegulatoryReportDefinitions to ship").</summary>
public enum RegulatoryReportCategory
{
    StudentEnrollment,
    GenderDistribution,
    ProgramStatistics,
    FacultyStaffStatistics,
    Graduation,
    AcademicPerformance,
    Research,
    FinancialInformation,
    Infrastructure,
    Scholarships,
    InternationalStudents,
}
