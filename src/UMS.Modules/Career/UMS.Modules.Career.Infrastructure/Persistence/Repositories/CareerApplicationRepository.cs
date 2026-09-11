using Microsoft.EntityFrameworkCore;
using Npgsql;
using UMS.Modules.Career.Application.Abstractions;
using UMS.Modules.Career.Domain.Applications;
using UMS.Modules.Career.Domain.Drives;
using UMS.Modules.Career.Domain.Internships;

namespace UMS.Modules.Career.Infrastructure.Persistence.Repositories;

/// <summary>
/// CAR-6/CAR-11/CAR-12/CAR-13: design-decisions.md "Internship/Drive Application-Window Write-Time
/// Enforcement" and "Interview-Slot Booking Concurrency Control" - every guarded-insert/atomic-UPDATE
/// method here is a single, atomic Postgres statement whose WHERE clause is evaluated against the row
/// as it stands at the instant of the write itself, mirroring Alumni's own
/// <c>JobApplicationRepository.TryInsertIfPostingAcceptsApplicationsAsync</c> exactly.
/// </summary>
internal sealed class CareerApplicationRepository(CareerDbContext context) : ICareerApplicationRepository
{
    private static readonly HashSet<CareerApplicationStatus> TerminalStatuses =
    [
        CareerApplicationStatus.Offered,
        CareerApplicationStatus.Rejected,
        CareerApplicationStatus.Withdrawn,
        CareerApplicationStatus.Cancelled,
    ];

    public Task<CareerApplication?> GetByIdAsync(CareerApplicationId id, CancellationToken cancellationToken = default) =>
        context.CareerApplications.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    /// <summary>See <see cref="CareerApplicationBookingInfo"/>'s own remarks - deliberately `AsNoTracking`.</summary>
    public Task<CareerApplicationBookingInfo?> GetBookingInfoAsync(CareerApplicationId id, CancellationToken cancellationToken = default) =>
        context.CareerApplications.AsNoTracking()
            .Where(a => a.Id == id)
            .Select(a => new CareerApplicationBookingInfo(a.StudentId, a.DriveId!.Value.Value, a.Status.ToString(), a.InterviewSlotId))
            .FirstOrDefaultAsync(cancellationToken);

    public Task<IReadOnlyList<CareerApplication>> ListByStudentAsync(Guid studentId, CancellationToken cancellationToken = default) =>
        ToListAsync(context.CareerApplications.Where(a => a.StudentId == studentId).OrderByDescending(a => a.SubmittedAt), cancellationToken);

    public Task<IReadOnlyList<CareerApplication>> ListByInternshipAsync(InternshipId internshipId, CancellationToken cancellationToken = default) =>
        ToListAsync(context.CareerApplications.Where(a => a.InternshipId == internshipId).OrderByDescending(a => a.SubmittedAt), cancellationToken);

    public Task<IReadOnlyList<CareerApplication>> ListByDriveAsync(CampusRecruitmentDriveId driveId, CancellationToken cancellationToken = default) =>
        ToListAsync(context.CareerApplications.Where(a => a.DriveId == driveId).OrderByDescending(a => a.SubmittedAt), cancellationToken);

    public Task<IReadOnlyList<CareerApplication>> ListNonTerminalByInternshipAsync(InternshipId internshipId, CancellationToken cancellationToken = default) =>
        ToListAsync(context.CareerApplications.Where(a => a.InternshipId == internshipId && !TerminalStatuses.Contains(a.Status)), cancellationToken);

    public Task<IReadOnlyList<CareerApplication>> ListNonTerminalByDriveAsync(CampusRecruitmentDriveId driveId, CancellationToken cancellationToken = default) =>
        ToListAsync(context.CareerApplications.Where(a => a.DriveId == driveId && !TerminalStatuses.Contains(a.Status)), cancellationToken);

    public async Task<bool> TryInsertIfInternshipAcceptsApplicationsAsync(CareerApplication application, CancellationToken cancellationToken = default)
    {
        try
        {
            var affected = await context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                 INSERT INTO career.career_applications
                     (id, student_id, internship_id, drive_id, status, declared_cgpa, declared_year_of_study,
                      resume_profile_id_snapshot, resume_artifact_id_snapshot, resume_file_name_snapshot, resume_snapshot_at,
                      interview_slot_id, submitted_at, decision_reason, decided_at)
                 SELECT {application.Id.Value}, {application.StudentId}, {application.InternshipId!.Value.Value}, NULL, {application.Status.ToString()},
                        {application.DeclaredCgpa}, {application.DeclaredYearOfStudy},
                        {application.ResumeProfileIdSnapshot}, {application.ResumeArtifactIdSnapshot}, {application.ResumeFileNameSnapshot}, {application.ResumeSnapshotAt},
                        NULL, {application.SubmittedAt}, NULL, NULL
                 WHERE EXISTS (
                     SELECT 1 FROM career.internships
                     WHERE id = {application.InternshipId!.Value.Value} AND status = 'ApplicationsOpen' AND application_deadline > now()
                 )
                 AND NOT EXISTS (
                     SELECT 1 FROM career.career_applications
                     WHERE student_id = {application.StudentId} AND internship_id = {application.InternshipId!.Value.Value}
                       AND status NOT IN ('Withdrawn', 'Cancelled')
                 )
                 """,
                cancellationToken).ConfigureAwait(false);

            return affected == 1;
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            // A genuine concurrent-duplicate race that slipped past the NOT EXISTS check - the
            // partial unique index (defense-in-depth) is the final word; treated identically to
            // "the guarded insert found nothing to do."
            return false;
        }
    }

    public async Task<bool> TryInsertIfDriveAcceptsRegistrationsAsync(CareerApplication application, CancellationToken cancellationToken = default)
    {
        try
        {
            var affected = await context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                 INSERT INTO career.career_applications
                     (id, student_id, internship_id, drive_id, status, declared_cgpa, declared_year_of_study,
                      resume_profile_id_snapshot, resume_artifact_id_snapshot, resume_file_name_snapshot, resume_snapshot_at,
                      interview_slot_id, submitted_at, decision_reason, decided_at)
                 SELECT {application.Id.Value}, {application.StudentId}, NULL, {application.DriveId!.Value.Value}, {application.Status.ToString()},
                        {application.DeclaredCgpa}, {application.DeclaredYearOfStudy},
                        {application.ResumeProfileIdSnapshot}, {application.ResumeArtifactIdSnapshot}, {application.ResumeFileNameSnapshot}, {application.ResumeSnapshotAt},
                        NULL, {application.SubmittedAt}, NULL, NULL
                 WHERE EXISTS (
                     SELECT 1 FROM career.campus_recruitment_drives
                     WHERE id = {application.DriveId!.Value.Value} AND status = 'RegistrationOpen' AND registration_closes_at > now()
                 )
                 AND NOT EXISTS (
                     SELECT 1 FROM career.career_applications
                     WHERE student_id = {application.StudentId} AND drive_id = {application.DriveId!.Value.Value}
                       AND status NOT IN ('Withdrawn', 'Cancelled')
                 )
                 """,
                cancellationToken).ConfigureAwait(false);

            return affected == 1;
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return false;
        }
    }

    public async Task<bool> TryAssignSlotAsync(CareerApplicationId applicationId, Guid slotId, CancellationToken cancellationToken = default)
    {
        var affected = await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
             UPDATE career.career_applications
             SET interview_slot_id = {slotId}, status = 'InterviewScheduled'
             WHERE id = {applicationId.Value} AND interview_slot_id IS NULL AND status = 'Shortlisted'
             """,
            cancellationToken).ConfigureAwait(false);

        return affected == 1;
    }

    public async Task<bool> TryReleaseSlotAsync(CareerApplicationId applicationId, CancellationToken cancellationToken = default)
    {
        var affected = await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
             UPDATE career.career_applications
             SET interview_slot_id = NULL, status = 'Shortlisted'
             WHERE id = {applicationId.Value} AND interview_slot_id IS NOT NULL AND status = 'InterviewScheduled'
             """,
            cancellationToken).ConfigureAwait(false);

        return affected == 1;
    }

    public void Add(CareerApplication application) => context.CareerApplications.Add(application);

    private static async Task<IReadOnlyList<CareerApplication>> ToListAsync(IQueryable<CareerApplication> query, CancellationToken cancellationToken) =>
        await query.ToListAsync(cancellationToken).ConfigureAwait(false);
}
