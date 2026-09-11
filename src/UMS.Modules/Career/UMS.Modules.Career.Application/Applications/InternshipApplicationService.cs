using UMS.Modules.Career.Application.Abstractions;
using UMS.Modules.Career.Domain.Applications;
using UMS.Modules.Career.Domain.Internships;
using UMS.Shared.ErrorHandling.Results;
using UMS.Shared.Student;

namespace UMS.Modules.Career.Application.Applications;

/// <summary>
/// CAR-6: `POST /internships/{id}/apply` (requirement-spec.md §2.4, §2.5). design-decisions.md
/// "Internship/Drive Application-Window Write-Time Enforcement": the write-time guard lives entirely
/// in <see cref="ICareerApplicationRepository.TryInsertIfInternshipAcceptsApplicationsAsync"/>'s own
/// guarded INSERT - this service never trusts an earlier read of the Internship's own status as the
/// basis for the write. design-decisions.md "Student-Graduation Boundary": `Student.status = Active`
/// is checked here via a FRESH, live call to `IStudentStatusChecker` - never cached, never assumed.
/// </summary>
public sealed class InternshipApplicationService(
    IInternshipRepository internships,
    IResumeProfileRepository resumeProfiles,
    ICareerApplicationRepository applications,
    IStudentStatusChecker studentStatusChecker,
    IUnitOfWork unitOfWork,
    IDomainEventRecorder domainEventRecorder,
    IClock clock)
{
    public async Task<Result<CareerApplicationDto>> ApplyAsync(Guid identityUserId, Guid internshipId, ApplyToInternshipRequest request, CancellationToken cancellationToken = default)
    {
        var standing = await studentStatusChecker.GetByUserIdAsync(identityUserId, cancellationToken).ConfigureAwait(false);
        if (standing is null)
        {
            return Error.Failure("careerapplication.student_unresolved", "Could not resolve a Student record for the current user.");
        }

        if (!string.Equals(standing.Status, "Active", StringComparison.OrdinalIgnoreCase))
        {
            return Error.Forbidden("careerapplication.student_not_active", "Only an Active Student may submit a new CareerApplication (requirement-spec.md §2.5).");
        }

        var internship = await internships.GetByIdAsync(new InternshipId(internshipId), cancellationToken).ConfigureAwait(false);
        if (internship is null)
        {
            return Error.NotFound("internship.not_found", $"No Internship exists with id '{internshipId}'.");
        }

        if (!internship.Eligibility.IsSatisfiedByProgram(standing.ProgramId) || !internship.Eligibility.IsSatisfiedByDeclaredValues(request.DeclaredCgpa, request.DeclaredYearOfStudy))
        {
            return Error.Forbidden("careerapplication.not_eligible", "This Student does not meet the Internship's EligibilityCriteria.");
        }

        var resumeProfile = await resumeProfiles.GetByIdAsync(new Domain.ResumeProfiles.ResumeProfileId(request.ResumeProfileId), cancellationToken).ConfigureAwait(false);
        if (resumeProfile is null || resumeProfile.StudentId != standing.StudentId || resumeProfile.IsDeleted)
        {
            return Error.NotFound("resumeprofile.not_found", $"No ResumeProfile exists with id '{request.ResumeProfileId}' for this Student.");
        }

        var now = clock.UtcNow;
        var resumeSnapshot = new ResumeSnapshot(resumeProfile.Id.Value, resumeProfile.ArtifactId, resumeProfile.FileName, now);
        var application = CareerApplication.SubmitForInternship(standing.StudentId, new InternshipId(internshipId), request.DeclaredCgpa, request.DeclaredYearOfStudy, resumeSnapshot, now);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var inserted = await applications.TryInsertIfInternshipAcceptsApplicationsAsync(application, cancellationToken).ConfigureAwait(false);
        if (!inserted)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Error.Conflict("internship.not_accepting_applications", $"Internship '{internshipId}' is not currently accepting applications, or this Student already has a non-terminal CareerApplication against it.");
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
