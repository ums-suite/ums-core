using UMS.Modules.Career.Domain.Applications;

namespace UMS.Modules.Career.Application.Applications;

internal static class CareerApplicationMapper
{
    public static CareerApplicationDto ToDto(CareerApplication application) => new(
        application.Id.Value,
        application.StudentId,
        application.InternshipId?.Value,
        application.DriveId?.Value,
        application.Status.ToString(),
        application.DeclaredCgpa,
        application.DeclaredYearOfStudy,
        application.ResumeProfileIdSnapshot,
        application.ResumeArtifactIdSnapshot,
        application.ResumeFileNameSnapshot,
        application.InterviewSlotId,
        application.SubmittedAt,
        application.DecisionReason,
        application.DecidedAt,
        application.Version);
}
