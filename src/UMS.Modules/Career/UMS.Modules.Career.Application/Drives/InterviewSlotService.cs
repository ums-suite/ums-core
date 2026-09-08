using UMS.Modules.Career.Application.Abstractions;
using UMS.Modules.Career.Application.Applications;
using UMS.Modules.Career.Domain.Applications;
using UMS.Modules.Career.Domain.Drives;
using UMS.Modules.Career.Domain.Events;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Career.Application.Drives;

/// <summary>
/// CAR-12/CAR-13: `InterviewSlot` definition and the module's highest-concurrency write path
/// (requirement-spec.md §2.3, §5 Concurrency NFR). design-decisions.md "Interview-Slot Booking
/// Concurrency Control": <see cref="BookAsync"/> is a single DB transaction wrapping TWO atomic
/// conditional-UPDATE statements - the slot's own capacity claim
/// (<see cref="IInterviewSlotRepository.TryClaimSlotAsync"/>) and the
/// <c>CareerApplication.interview_slot_id IS NULL AND status = 'Shortlisted'</c> guard
/// (<see cref="ICareerApplicationRepository.TryAssignSlotAsync"/>) - never a
/// <c>SELECT ... FOR UPDATE</c> lock-then-check (edge-cases.md's residual note: holding a row lock
/// across a human's click-to-confirm interaction would add contention without any additional
/// correctness benefit).
///
/// <para>
/// ums-core-gotchas "stale-tracked-entity-after-raw-SQL": pre-flight reads here use
/// <see cref="ICareerApplicationRepository.GetBookingInfoAsync"/>'s deliberately untracked
/// projection, never a tracked `CareerApplication` instance that the subsequent raw-SQL UPDATE would
/// silently leave stale in the `DbContext`'s change tracker.
/// </para>
/// </summary>
public sealed class InterviewSlotService(
    IInterviewSlotRepository slots,
    ICareerApplicationRepository applications,
    IUnitOfWork unitOfWork,
    IDomainEventRecorder domainEventRecorder,
    IClock clock)
{
    public async Task<Result<IReadOnlyList<InterviewSlotDto>>> DefineSlotsAsync(Guid driveId, DefineInterviewSlotsRequest request, CancellationToken cancellationToken = default)
    {
        var created = new List<InterviewSlot>();
        try
        {
            foreach (var slot in request.Slots)
            {
                created.Add(InterviewSlot.Create(new CampusRecruitmentDriveId(driveId), slot.StartTime, slot.EndTime, slot.Capacity));
            }
        }
        catch (ArgumentException ex)
        {
            return Error.Validation("interviewslot.invalid", ex.Message);
        }

        slots.AddRange(created);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return created.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<InterviewSlotDto>> ListByDriveAsync(Guid driveId, CancellationToken cancellationToken = default) =>
        (await slots.ListByDriveAsync(new CampusRecruitmentDriveId(driveId), cancellationToken).ConfigureAwait(false)).Select(ToDto).ToList();

    /// <summary>CAR-12: the concurrency-sensitive booking write. Rejects immediately (no queueing) if the race is lost - design-decisions.md's accepted trade-off.</summary>
    public async Task<Result<CareerApplicationDto>> BookAsync(Guid driveId, Guid slotId, Guid careerApplicationId, Guid callerStudentId, CancellationToken cancellationToken = default)
    {
        var bookingInfo = await applications.GetBookingInfoAsync(new CareerApplicationId(careerApplicationId), cancellationToken).ConfigureAwait(false);
        if (bookingInfo is null || bookingInfo.DriveId != driveId)
        {
            return Error.NotFound("careerapplication.not_found", $"No CareerApplication exists with id '{careerApplicationId}' against CampusRecruitmentDrive '{driveId}'.");
        }

        if (bookingInfo.StudentId != callerStudentId)
        {
            return Error.Forbidden("careerapplication.not_owner", "Only the Student who submitted this CareerApplication may book an InterviewSlot for it.");
        }

        var slot = await slots.GetByIdAsync(new InterviewSlotId(slotId), cancellationToken).ConfigureAwait(false);
        if (slot is null || slot.DriveId.Value != driveId)
        {
            return Error.NotFound("interviewslot.not_found", $"No InterviewSlot exists with id '{slotId}' on CampusRecruitmentDrive '{driveId}'.");
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var claimed = await slots.TryClaimSlotAsync(new InterviewSlotId(slotId), cancellationToken).ConfigureAwait(false);
        if (!claimed)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Error.Conflict("interviewslot.no_capacity", $"InterviewSlot '{slotId}' has no remaining capacity.");
        }

        var assigned = await applications.TryAssignSlotAsync(new CareerApplicationId(careerApplicationId), slotId, cancellationToken).ConfigureAwait(false);
        if (!assigned)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Error.Conflict("careerapplication.slot_assignment_failed", $"CareerApplication '{careerApplicationId}' is not currently Shortlisted, or already has a booked InterviewSlot.");
        }

        var now = clock.UtcNow;
        domainEventRecorder.Enqueue(new InterviewSlotBooked(slotId, driveId, careerApplicationId, bookingInfo.StudentId, now));
        domainEventRecorder.Enqueue(new CareerApplicationStatusChanged(careerApplicationId, bookingInfo.StudentId, CareerApplicationStatus.Shortlisted.ToString(), CareerApplicationStatus.InterviewScheduled.ToString(), now));

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        return new CareerApplicationDto(
            careerApplicationId,
            bookingInfo.StudentId,
            null,
            driveId,
            CareerApplicationStatus.InterviewScheduled.ToString(),
            null,
            null,
            Guid.Empty,
            Guid.Empty,
            string.Empty,
            slotId,
            now,
            null,
            null,
            0);
    }

    /// <summary>CAR-13: a Student releases a booked slot - the atomic decrement makes it immediately bookable again (edge-cases.md, "no separate waitlist mechanism exists in v1").</summary>
    public async Task<Result> CancelBookingAsync(Guid driveId, Guid slotId, Guid careerApplicationId, Guid callerStudentId, CancellationToken cancellationToken = default)
    {
        var bookingInfo = await applications.GetBookingInfoAsync(new CareerApplicationId(careerApplicationId), cancellationToken).ConfigureAwait(false);
        if (bookingInfo is null || bookingInfo.DriveId != driveId)
        {
            return Result.Failure(Error.NotFound("careerapplication.not_found", $"No CareerApplication exists with id '{careerApplicationId}' against CampusRecruitmentDrive '{driveId}'."));
        }

        if (bookingInfo.StudentId != callerStudentId)
        {
            return Result.Failure(Error.Forbidden("careerapplication.not_owner", "Only the Student who booked this InterviewSlot may cancel it."));
        }

        if (bookingInfo.InterviewSlotId != slotId)
        {
            return Result.Failure(Error.Conflict("careerapplication.slot_not_booked", $"CareerApplication '{careerApplicationId}' does not currently hold a booking on InterviewSlot '{slotId}'."));
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var released = await applications.TryReleaseSlotAsync(new CareerApplicationId(careerApplicationId), cancellationToken).ConfigureAwait(false);
        if (!released)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Result.Failure(Error.Conflict("careerapplication.slot_not_booked", $"CareerApplication '{careerApplicationId}' does not currently hold a booked InterviewSlot."));
        }

        await slots.ReleaseSlotAsync(new InterviewSlotId(slotId), cancellationToken).ConfigureAwait(false);

        var now = clock.UtcNow;
        domainEventRecorder.Enqueue(new InterviewSlotCancelled(slotId, driveId, careerApplicationId, bookingInfo.StudentId, now));

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    private static InterviewSlotDto ToDto(InterviewSlot slot) => new(slot.Id.Value, slot.DriveId.Value, slot.StartTime, slot.EndTime, slot.Capacity, slot.BookedCount, slot.IsCancelled);
}
