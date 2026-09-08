using UMS.Modules.Career.Application.Abstractions;
using UMS.Modules.Career.Domain.Applications;
using UMS.Modules.Career.Domain.Drives;
using UMS.Shared.ErrorHandling.Results;
using UMS.Shared.Student;

namespace UMS.Modules.Career.Application.Applications;

/// <summary>
/// CAR-11/CAR-14: `POST /drives/{id}/register` - requirement-spec.md §3's aggregate table names no
/// separate registration entity, so "registering interest" in a Drive directly creates a
/// `CareerApplication` targeting `drive_id` (status `Submitted`), reusing CAR-6's exact guarded-insert
/// shape against `CampusRecruitmentDrive.RegistrationOpensAt/RegistrationClosesAt` instead of an
/// Internship's own deadline. No `EligibilityCriteria` dimension applies to a Drive (requirement-spec
/// §2.2 scopes that value object to `Internship` only) - only the `Student.status = Active` gate.
/// </summary>
public sealed class DriveApplicationService(
    ICampusRecruitmentDriveRepository drives,
    IResumeProfileRepository resumeProfiles,
    ICareerApplicationRepository applications,
    IStudentStatusChecker studentStatusChecker,
    IUnitOfWork unitOfWork,
    IDomainEventRecorder domainEventRecorder,
    IClock clock)
{
    public async Task<Result<CareerApplicationDto>> RegisterAsync(Guid identityUserId, Guid driveId, RegisterForDriveRequest request, CancellationToken cancellationToken = default)
    {
        var standing = await studentStatusChecker.GetByUserIdAsync(identityUserId, cancellationToken).ConfigureAwait(false);
        if (standing is null)
        {
            return Error.Failure("careerapplication.student_unresolved", "Could not resolve a Student record for the current user.");
        }

        if (!string.Equals(standing.Status, "Active", StringComparison.OrdinalIgnoreCase))
        {
            return Error.Forbidden("careerapplication.student_not_active", "Only an Active Student may register for a new CampusRecruitmentDrive (requirement-spec.md §2.5).");
        }

        var drive = await drives.GetByIdAsync(new CampusRecruitmentDriveId(driveId), cancellationToken).ConfigureAwait(false);
        if (drive is null)
        {
            return Error.NotFound("campusrecruitmentdrive.not_found", $"No CampusRecruitmentDrive exists with id '{driveId}'.");
        }

        var resumeProfile = await resumeProfiles.GetByIdAsync(new Domain.ResumeProfiles.ResumeProfileId(request.ResumeProfileId), cancellationToken).ConfigureAwait(false);
        if (resumeProfile is null || resumeProfile.StudentId != standing.StudentId || resumeProfile.IsDeleted)
        {
            return Error.NotFound("resumeprofile.not_found", $"No ResumeProfile exists with id '{request.ResumeProfileId}' for this Student.");
        }

        var now = clock.UtcNow;
        var resumeSnapshot = new ResumeSnapshot(resumeProfile.Id.Value, resumeProfile.ArtifactId, resumeProfile.FileName, now);
        var application = CareerApplication.SubmitForDrive(standing.StudentId, new CampusRecruitmentDriveId(driveId), request.DeclaredCgpa, request.DeclaredYearOfStudy, resumeSnapshot, now);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var inserted = await applications.TryInsertIfDriveAcceptsRegistrationsAsync(application, cancellationToken).ConfigureAwait(false);
        if (!inserted)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Error.Conflict("campusrecruitmentdrive.not_accepting_registrations", $"CampusRecruitmentDrive '{driveId}' is not currently open for registration, or this Student already has a non-terminal CareerApplication against it.");
        }

        foreach (var domainEvent in application.DomainEvents)
        {
            domainEventRecorder.Enqueue(domainEvent);
        }

        application.ClearDomainEvents();
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        return CareerApplicationMapper.ToDto(application);
    }
}
