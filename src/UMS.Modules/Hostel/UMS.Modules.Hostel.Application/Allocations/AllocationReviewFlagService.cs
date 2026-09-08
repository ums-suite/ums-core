using UMS.Modules.Hostel.Application.Abstractions;
using UMS.Modules.Hostel.Application.Common;
using UMS.Modules.Hostel.Domain.Allocations;

namespace UMS.Modules.Hostel.Application.Allocations;

/// <summary>
/// HOS-17: requirement-spec.md §8 edge case "Student's status changes to Suspended/Graduated
/// mid-allocation... Allocation is flagged for officer-initiated checkout, not auto-terminated
/// silently." design-decisions.md "Student-Status-Change Review Flag - Additive Side-Table":
/// insert-only, never a mutable field on <see cref="Allocation"/> - advisory only, never itself
/// gates or forces a transition. Called by <c>HostelStudentStatusRelayWorker</c> for every
/// unprocessed <c>StudentStatusChanged</c> event in Student's own outbox.
/// </summary>
public sealed class AllocationReviewFlagService(
    IAllocationRepository allocations,
    IAllocationReviewFlagRepository reviewFlags,
    StudentContextService studentContext,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    private static readonly string[] FlagWorthyStatuses = ["Suspended", "Graduated"];

    /// <summary>See <see cref="StudentStatusEventEnvelope"/>'s own remarks on why the CURRENT status is re-resolved here rather than trusting the source event's own payload value.</summary>
    public async Task ApplyStudentStatusChangeAsync(Guid studentId, string sourceEventReference, CancellationToken cancellationToken = default)
    {
        var standing = await studentContext.GetStandingAsync(studentId, cancellationToken).ConfigureAwait(false);
        if (standing is null || !FlagWorthyStatuses.Contains(standing.Status, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        var newStatus = standing.Status;
        var studentAllocations = await allocations.GetByStudentAsync(studentId, cancellationToken).ConfigureAwait(false);
        var openAllocations = studentAllocations.Where(a => a.Status is AllocationStatus.Pending or AllocationStatus.FeePaid or AllocationStatus.Active).ToList();
        if (openAllocations.Count == 0)
        {
            return;
        }

        var now = clock.UtcNow;
        foreach (var allocation in openAllocations)
        {
            var flag = AllocationReviewFlag.Create(
                allocation.Id.Value,
                $"Student status changed to '{newStatus}' - review for officer-initiated checkout.",
                sourceEventReference,
                now);
            if (flag.IsSuccess)
            {
                reviewFlags.Add(flag.Value);
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<AllocationReviewFlagDto>> GetByAllocationAsync(Guid allocationId, CancellationToken cancellationToken = default) =>
        (await reviewFlags.GetByAllocationAsync(allocationId, cancellationToken).ConfigureAwait(false))
            .Select(f => new AllocationReviewFlagDto(f.Id, f.AllocationId, f.Reason, f.SourceEventReference, f.CreatedAt))
            .ToList();
}

public sealed record AllocationReviewFlagDto(Guid Id, Guid AllocationId, string Reason, string SourceEventReference, DateTimeOffset CreatedAt);
