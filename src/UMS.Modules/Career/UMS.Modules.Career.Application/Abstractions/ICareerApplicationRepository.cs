using UMS.Modules.Career.Domain.Applications;
using UMS.Modules.Career.Domain.Drives;
using UMS.Modules.Career.Domain.Internships;

namespace UMS.Modules.Career.Application.Abstractions;

public interface ICareerApplicationRepository
{
    public Task<CareerApplication?> GetByIdAsync(CareerApplicationId id, CancellationToken cancellationToken = default);

    /// <summary>See <see cref="CareerApplicationBookingInfo"/>'s own remarks - an untracked projection used only around CAR-12/CAR-13's raw-SQL writes.</summary>
    public Task<CareerApplicationBookingInfo?> GetBookingInfoAsync(CareerApplicationId id, CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<CareerApplication>> ListByStudentAsync(Guid studentId, CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<CareerApplication>> ListByInternshipAsync(InternshipId internshipId, CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<CareerApplication>> ListByDriveAsync(CampusRecruitmentDriveId driveId, CancellationToken cancellationToken = default);

    /// <summary>CAR-9/CAR-15: every non-terminal CareerApplication against a withdrawn/cancelled posting - the withdrawal cascade's own fan-out read.</summary>
    public Task<IReadOnlyList<CareerApplication>> ListNonTerminalByInternshipAsync(InternshipId internshipId, CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<CareerApplication>> ListNonTerminalByDriveAsync(CampusRecruitmentDriveId driveId, CancellationToken cancellationToken = default);

    /// <summary>
    /// CAR-6: edge-cases.md "An Internship's ApplicationsClosed sweep racing a concurrent submission" -
    /// design-decisions.md "Internship/Drive Application-Window Write-Time Enforcement". A single
    /// guarded <c>INSERT ... SELECT ... WHERE</c> statement that re-checks
    /// <c>status = 'ApplicationsOpen' AND application_deadline > now()</c> and the
    /// at-most-one-non-terminal-application-per-(student,internship) invariant in the SAME statement
    /// as the insert itself - mirrors Alumni's own
    /// <c>IJobApplicationRepository.TryInsertIfPostingAcceptsApplicationsAsync</c> exactly. Returns
    /// <see langword="true"/> only if the row was actually inserted.
    /// </summary>
    public Task<bool> TryInsertIfInternshipAcceptsApplicationsAsync(CareerApplication application, CancellationToken cancellationToken = default);

    /// <summary>CAR-11/CAR-14: the identical guarded-insert mechanism, applied to a Drive registration/application targeting `drive_id` instead of `internship_id`.</summary>
    public Task<bool> TryInsertIfDriveAcceptsRegistrationsAsync(CareerApplication application, CancellationToken cancellationToken = default);

    /// <summary>
    /// CAR-12: design-decisions.md "Interview-Slot Booking Concurrency Control" - "the SAME transaction
    /// also guards <c>CareerApplication.interview_slot_id IS NULL AND status = 'Shortlisted'</c> so a
    /// Student can't book two slots for one Drive concurrently." A single atomic conditional UPDATE;
    /// returns <see langword="true"/> only if it actually affected a row.
    /// </summary>
    public Task<bool> TryAssignSlotAsync(CareerApplicationId applicationId, Guid slotId, CancellationToken cancellationToken = default);

    /// <summary>The symmetric release for <see cref="TryAssignSlotAsync"/> (CAR-13).</summary>
    public Task<bool> TryReleaseSlotAsync(CareerApplicationId applicationId, CancellationToken cancellationToken = default);

    public void Add(CareerApplication application);
}
