namespace UMS.Modules.Academic.Domain.Enrollments;

/// <summary>requirement-spec.md §2 Semester Registration - `Pending` only exists for Programs configured with an Advisor-approval gate (`Program.RequiresAdvisorApproval`); otherwise Enrollment is created directly `Active`.</summary>
public enum EnrollmentStatus
{
    Pending,
    Active,
    Dropped,
    Completed,
}
