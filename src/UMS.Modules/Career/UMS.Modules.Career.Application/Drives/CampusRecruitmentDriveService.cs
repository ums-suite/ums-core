using UMS.Modules.Career.Application.Abstractions;
using UMS.Modules.Career.Domain.Drives;
using UMS.Shared.ErrorHandling.Results;
using UMS.Shared.Organization;

namespace UMS.Modules.Career.Application.Drives;

/// <summary>CAR-10: CampusRecruitmentDrive lifecycle (requirement-spec.md §2.3, §3, §4). Venue resolves via Organization's own `IRoomExistenceChecker` (requirement-spec.md §7) - never a direct schema query, never a competing Career-owned venue concept.</summary>
public sealed class CampusRecruitmentDriveService(ICampusRecruitmentDriveRepository drives, IRoomExistenceChecker roomExistenceChecker, IUnitOfWork unitOfWork, IClock clock)
{
    public static CampusRecruitmentDriveDto ToDto(CampusRecruitmentDrive drive) => new(
        drive.Id.Value,
        drive.EmployerProfileId,
        drive.Title,
        drive.VenueRoomId,
        drive.ScheduledDate,
        drive.RegistrationOpensAt,
        drive.RegistrationClosesAt,
        drive.Status.ToString(),
        drive.CancellationReason,
        drive.CreatedAt,
        drive.Version);

    public async Task<Result<CampusRecruitmentDriveDto>> CreateAsync(CreateDriveRequest request, CancellationToken cancellationToken = default)
    {
        if (!await roomExistenceChecker.ExistsAsync(request.VenueRoomId, cancellationToken).ConfigureAwait(false))
        {
            return Error.Validation("campusrecruitmentdrive.venue_not_found", $"No Room exists with id '{request.VenueRoomId}'.");
        }

        CampusRecruitmentDrive drive;
        try
        {
            drive = CampusRecruitmentDrive.Create(request.EmployerProfileId, request.Title, request.VenueRoomId, request.ScheduledDate, request.RegistrationOpensAt, request.RegistrationClosesAt, clock.UtcNow);
        }
        catch (ArgumentException ex)
        {
            return Error.Validation("campusrecruitmentdrive.invalid", ex.Message);
        }

        drives.Add(drive);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDto(drive);
    }

    public async Task<Result<CampusRecruitmentDriveDto>> EditAsync(Guid id, EditDriveRequest request, CancellationToken cancellationToken = default)
    {
        var drive = await drives.GetByIdAsync(new CampusRecruitmentDriveId(id), cancellationToken).ConfigureAwait(false);
        if (drive is null)
        {
            return Error.NotFound("campusrecruitmentdrive.not_found", $"No CampusRecruitmentDrive exists with id '{id}'.");
        }

        if (!await roomExistenceChecker.ExistsAsync(request.VenueRoomId, cancellationToken).ConfigureAwait(false))
        {
            return Error.Validation("campusrecruitmentdrive.venue_not_found", $"No Room exists with id '{request.VenueRoomId}'.");
        }

        try
        {
            drive.Edit(request.Title, request.VenueRoomId, request.ScheduledDate, request.RegistrationOpensAt, request.RegistrationClosesAt);
        }
        catch (ArgumentException ex)
        {
            return Error.Validation("campusrecruitmentdrive.invalid", ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Error.Conflict("campusrecruitmentdrive.invalid_transition", ex.Message);
        }

        unitOfWork.SetExpectedVersion(drive, request.Version);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (ConcurrencyConflictException ex)
        {
            return Error.Conflict("campusrecruitmentdrive.concurrency_conflict", ex.Message);
        }

        return ToDto(drive);
    }

    public Task<Result<CampusRecruitmentDriveDto>> ScheduleAsync(Guid id, uint version, CancellationToken cancellationToken = default) =>
        TransitionAsync(id, version, d => d.Schedule(clock.UtcNow), cancellationToken);

    public Task<Result<CampusRecruitmentDriveDto>> OpenRegistrationAsync(Guid id, uint version, CancellationToken cancellationToken = default) =>
        TransitionAsync(id, version, d => d.OpenRegistration(), cancellationToken);

    public Task<Result<CampusRecruitmentDriveDto>> CloseRegistrationAsync(Guid id, uint version, CancellationToken cancellationToken = default) =>
        TransitionAsync(id, version, d => d.CloseRegistration(), cancellationToken);

    public Task<Result<CampusRecruitmentDriveDto>> CompleteAsync(Guid id, uint version, CancellationToken cancellationToken = default) =>
        TransitionAsync(id, version, d => d.Complete(), cancellationToken);

    /// <summary>CAR-15: cancellation itself - the cascade to affected `CareerApplication`s/booked slots is handled by `DriveCancellationCascadeHandler`, invoked by the caller AFTER this commits (design-decisions.md's in-process-event posture).</summary>
    public Task<Result<CampusRecruitmentDriveDto>> CancelAsync(Guid id, CancelDriveRequest request, CancellationToken cancellationToken = default) =>
        TransitionAsync(id, request.Version, d => d.Cancel(request.Reason, clock.UtcNow), cancellationToken);

    public async Task<Result<CampusRecruitmentDriveDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var drive = await drives.GetByIdAsync(new CampusRecruitmentDriveId(id), cancellationToken).ConfigureAwait(false);
        return drive is null ? Error.NotFound("campusrecruitmentdrive.not_found", $"No CampusRecruitmentDrive exists with id '{id}'.") : ToDto(drive);
    }

    public async Task<IReadOnlyList<CampusRecruitmentDriveDto>> ListAsync(Guid? employerProfileId, bool includeAllStatuses, int skip, int take, CancellationToken cancellationToken = default)
    {
        skip = Math.Max(skip, 0);
        take = Math.Clamp(take <= 0 ? 50 : take, 1, 200);
        var items = await drives.ListAsync(employerProfileId, includeAllStatuses, skip, take, cancellationToken).ConfigureAwait(false);
        return items.Select(ToDto).ToList();
    }

    private async Task<Result<CampusRecruitmentDriveDto>> TransitionAsync(Guid id, uint version, Action<CampusRecruitmentDrive> transition, CancellationToken cancellationToken)
    {
        var drive = await drives.GetByIdAsync(new CampusRecruitmentDriveId(id), cancellationToken).ConfigureAwait(false);
        if (drive is null)
        {
            return Error.NotFound("campusrecruitmentdrive.not_found", $"No CampusRecruitmentDrive exists with id '{id}'.");
        }

        try
        {
            transition(drive);
        }
        catch (ArgumentException ex)
        {
            return Error.Validation("campusrecruitmentdrive.invalid", ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Error.Conflict("campusrecruitmentdrive.invalid_transition", ex.Message);
        }

        unitOfWork.SetExpectedVersion(drive, version);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (ConcurrencyConflictException ex)
        {
            return Error.Conflict("campusrecruitmentdrive.concurrency_conflict", ex.Message);
        }

        return ToDto(drive);
    }
}
