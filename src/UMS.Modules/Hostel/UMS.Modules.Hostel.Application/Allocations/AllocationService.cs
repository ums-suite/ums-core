using System.Text.Json;
using UMS.Modules.Hostel.Application.Abstractions;
using UMS.Modules.Hostel.Application.Common;
using UMS.Modules.Hostel.Domain.Allocations;
using UMS.Modules.Hostel.Domain.Applications;
using UMS.Modules.Hostel.Domain.Hostels;
using UMS.Shared.Audit;
using UMS.Shared.ErrorHandling.Results;
using UMS.Shared.Finance;

namespace UMS.Modules.Hostel.Application.Allocations;

/// <summary>
/// HOS-7/8/11/12/13: the Bed-allocation-concurrency-safe command (the pivot every downstream
/// fee/check-in/check-out/complaint ticket depends on transitively), check-in, check-out, and the
/// Student's own allocation history read.
/// </summary>
public sealed class AllocationService(
    IHostelApplicationRepository applications,
    IAllocationRepository allocationRepository,
    IBedRepository beds,
    IInvoiceRequester invoiceRequester,
    StudentContextService studentContext,
    HostelOptions options,
    IUnitOfWork unitOfWork,
    IAuditRecorder auditRecorder,
    IClock clock)
{
    private const int MaxBedSearchAttemptsPerPreference = 8;

    /// <summary>
    /// HOS-6/HOS-7/HOS-8: the officer-approval composite - requirement-spec.md §2 steps 5-7.
    /// <b>Lock ordering (design-decisions.md): lock the HostelApplication row, then the target Bed
    /// row, inside the SAME transaction</b> - this is the one shared lock-then-transition pattern
    /// covering both the Bed-oversell race and the withdrawal-vs-approval race
    /// (<see cref="Applications.HostelApplicationService.WithdrawAsync"/> takes the identical
    /// Application-row lock).
    /// </summary>
    /// <param name="officerUserId">
    /// The reviewing Officer's own user id for a human-initiated approval; <see langword="null"/>
    /// for HOS-14's system-triggered waitlist re-ranking offer, audited as a
    /// <c>system:waitlist-rerank</c> actor instead (<see cref="AuditContext.ForSystemJob"/>).
    /// </param>
    public async Task<Result<AllocationDto>> ApproveAndAllocateAsync(Guid applicationId, Guid? officerUserId, string correlationId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var application = await applications.GetByIdForUpdateAsync(new HostelApplicationId(applicationId), cancellationToken).ConfigureAwait(false);
        if (application is null)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Error.NotFound("hostel_application.not_found", $"No HostelApplication exists with id '{applicationId}'.");
        }

        if (application.Status is not (HostelApplicationStatus.Ranked or HostelApplicationStatus.Waitlisted))
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Error.Conflict("hostel_application.not_reviewable", $"HostelApplication '{applicationId}' cannot be approved - it is currently '{application.Status}'.");
        }

        if (application.IsEligible != true)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Error.Conflict("hostel_application.not_eligible", $"HostelApplication '{applicationId}' has not passed eligibility and cannot be approved.");
        }

        var bedSearch = await FindAndLockAvailableBedAsync(application.Preferences.OrderBy(p => p.Rank).Select(p => (p.HostelId, p.PreferredRoomType)), cancellationToken).ConfigureAwait(false);
        if (bedSearch is null)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Error.Conflict("allocation.no_bed_available", $"No available Bed matches any of HostelApplication '{applicationId}''s preferences right now.");
        }

        var (bedId, roomId, hostelId) = bedSearch.Value;
        var standing = await studentContext.GetStandingAsync(application.StudentId, cancellationToken).ConfigureAwait(false);
        if (standing?.IdentityUserId is null)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Error.Failure("allocation.student_identity_unresolved", $"Student '{application.StudentId}' has no linked Identity user - cannot raise a hostel-fee Invoice.");
        }

        var now = clock.UtcNow;
        var created = Allocation.Create(application.StudentId, bedId, roomId, hostelId, applicationId, now.AddDays(options.GracePeriodDays), now);
        if (created.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return created.Error!;
        }

        var allocation = created.Value;

        // HOS-8: a direct, synchronous in-process command call to Finance (ADR-0003) - participates
        // in the same transaction as the Allocation-approval write (design-decisions.md "Fee-Linked
        // Transition Consistency").
        var invoice = await invoiceRequester.CreateInvoiceAsync(
            new CreateInvoiceCommand("hostel", allocation.Id.Value.ToString(), "HostelFee", standing.IdentityUserId.Value, ApplicabilityReferenceId: null, officerUserId, correlationId),
            cancellationToken).ConfigureAwait(false);
        if (invoice.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return invoice.Error!;
        }

        allocation.RecordInvoice(invoice.Value.InvoiceId);
        allocationRepository.Add(allocation);

        var approved = application.Approve(now);
        if (approved.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return approved.Error!;
        }

        application.RecordAllocation(allocation.Id.Value);

        var afterJson = JsonSerializer.Serialize(new { studentId = allocation.StudentId, bedId = allocation.BedId, status = allocation.Status.ToString() });
        var audit = officerUserId is { } actorUserId
            ? new AuditContext(actorUserId, ActorIpAddress: null, correlationId).ToRequest("Allocation", allocation.Id.Value.ToString(), AuditActions.Create, beforeValueJson: null, afterJson)
            : AuditContext.ForSystemJob("waitlist-rerank", correlationId, "Allocation", allocation.Id.Value.ToString(), AuditActions.Create, beforeValueJson: null, afterJson);

        var committed = await Common.TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, audit, cancellationToken).ConfigureAwait(false);
        return committed.IsFailure ? committed.Error! : ToDto(allocation);
    }

    /// <summary>
    /// HOS-11: requirement-spec.md §4 - "Allocation cannot reach Active before FeePaid." No manual
    /// override path is built here (no requirement names its concrete audited-exception shape) -
    /// carried forward as a documented gap rather than guessed at.
    /// </summary>
    public async Task<Result<AllocationDto>> CheckInAsync(Guid allocationId, Guid officerUserId, string correlationId, CancellationToken cancellationToken = default)
    {
        var allocation = await allocationRepository.GetByIdAsync(new AllocationId(allocationId), cancellationToken).ConfigureAwait(false);
        if (allocation is null)
        {
            return Error.NotFound("allocation.not_found", $"No Allocation exists with id '{allocationId}'.");
        }

        var statusBefore = allocation.Status;
        var checkedIn = allocation.CheckIn(clock.UtcNow);
        if (checkedIn.IsFailure)
        {
            return checkedIn.Error!;
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var audit = new AuditContext(officerUserId, ActorIpAddress: null, correlationId)
            .ToRequest("Allocation", allocation.Id.Value.ToString(), AuditActions.Update, $"{{\"status\":\"{statusBefore}\"}}", "{\"status\":\"Active\"}");

        var committed = await Common.TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, audit, cancellationToken).ConfigureAwait(false);
        return committed.IsFailure ? committed.Error! : ToDto(allocation);
    }

    /// <summary>
    /// HOS-13: requirement-spec.md §2 Check-out. Takes the SAME <c>SELECT ... FOR UPDATE</c> lock
    /// discipline as <see cref="ApproveAndAllocateAsync"/>'s bed-claim, symmetric on the Bed-freeing
    /// side (design-decisions.md "Check-out and a new allocation racing for the same bed").
    /// </summary>
    public async Task<Result<AllocationDto>> CheckOutAsync(Guid allocationId, CheckOutType checkOutType, Guid actorUserId, string correlationId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var allocation = await allocationRepository.GetByIdAsync(new AllocationId(allocationId), cancellationToken).ConfigureAwait(false);
        if (allocation is null)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Error.NotFound("allocation.not_found", $"No Allocation exists with id '{allocationId}'.");
        }

        var lockedBed = await beds.GetByIdForUpdateAsync(new BedId(allocation.BedId), cancellationToken).ConfigureAwait(false);
        if (lockedBed is null)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Error.NotFound("bed.not_found", $"No Bed exists with id '{allocation.BedId}'.");
        }

        var statusBefore = allocation.Status;
        var checkedOut = allocation.CheckOut(checkOutType, clock.UtcNow);
        if (checkedOut.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return checkedOut.Error!;
        }

        // requirement-spec.md §2 Check-out: "Voluntary early check-out triggers a refund request to
        // Finance" - Hostel does not compute the proration itself (§9 Open Question, carried forward
        // as a documented gap). No synchronous IRefundRequester contract exists in
        // UMS.Shared.Finance today (unlike IInvoiceRequester) - the refund request is therefore
        // raised as the AllocationCheckedOut domain event's own RefundEligible flag (already the
        // event catalog's stated consumer: "Finance (refund-eligible flag)"), an async fan-out
        // Finance can poll for exactly the same way Hostel itself polls Finance's/Student's outbox
        // elsewhere in this module - not a new synchronous command call this ticket set builds.
        var audit = new AuditContext(actorUserId, ActorIpAddress: null, correlationId).ToRequest(
            "Allocation",
            allocation.Id.Value.ToString(),
            AuditActions.Update,
            $"{{\"status\":\"{statusBefore}\"}}",
            JsonSerializer.Serialize(new { status = allocation.Status.ToString(), checkOutType = checkOutType.ToString(), refundRequested = allocation.RefundRequested }));

        var committed = await Common.TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, audit, cancellationToken).ConfigureAwait(false);
        return committed.IsFailure ? committed.Error! : ToDto(allocation);
    }

    public async Task<Result<AllocationDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var allocation = await allocationRepository.GetByIdAsync(new AllocationId(id), cancellationToken).ConfigureAwait(false);
        return allocation is null
            ? Error.NotFound("allocation.not_found", $"No Allocation exists with id '{id}'.")
            : ToDto(allocation);
    }

    public async Task<IReadOnlyList<AllocationDto>> GetByStudentAsync(Guid studentId, CancellationToken cancellationToken = default) =>
        (await allocationRepository.GetByStudentAsync(studentId, cancellationToken).ConfigureAwait(false)).Select(ToDto).ToList();

    internal static AllocationDto ToDto(Allocation allocation) => new(
        allocation.Id.Value,
        allocation.StudentId,
        allocation.BedId,
        allocation.RoomId,
        allocation.HostelId,
        allocation.HostelApplicationId,
        allocation.Status.ToString(),
        allocation.InvoiceId,
        allocation.FeeGraceDeadline,
        allocation.CheckOutKind?.ToString(),
        allocation.RefundRequested,
        allocation.CreatedAt,
        allocation.FeePaidAt,
        allocation.ActivatedAt,
        allocation.CheckedOutAt,
        allocation.ExpiredAt);

    /// <summary>
    /// HOS-7/HOS-10/HOS-14's shared bed-finder: an unlocked candidate scan followed by a re-verified
    /// pessimistic lock, retried up to <see cref="MaxBedSearchAttemptsPerPreference"/> times per
    /// preference against a losing candidate before moving to the next preference - the "one Room
    /// row lock, then re-check" pattern extended to Bed search over several candidates instead of one.
    /// </summary>
    internal async Task<(Guid BedId, Guid RoomId, Guid HostelId)?> FindAndLockAvailableBedAsync(IEnumerable<(Guid HostelId, RoomType RoomType)> orderedPreferences, CancellationToken cancellationToken)
    {
        foreach (var (hostelId, roomType) in orderedPreferences)
        {
            var excluded = new List<Guid>();
            for (var attempt = 0; attempt < MaxBedSearchAttemptsPerPreference; attempt++)
            {
                var candidate = await beds.FindAvailableBedAsync(hostelId, roomType, excluded, cancellationToken).ConfigureAwait(false);
                if (candidate is null)
                {
                    break;
                }

                var lockedBed = await beds.GetByIdForUpdateAsync(new BedId(candidate.BedId), cancellationToken).ConfigureAwait(false);
                if (lockedBed is null)
                {
                    excluded.Add(candidate.BedId);
                    continue;
                }

                var stillFree = !await allocationRepository.BedHasActiveOrPendingAllocationAsync(candidate.BedId, cancellationToken).ConfigureAwait(false);
                if (stillFree)
                {
                    return (candidate.BedId, candidate.RoomId, hostelId);
                }

                excluded.Add(candidate.BedId);
            }
        }

        return null;
    }
}
