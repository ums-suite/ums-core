using UMS.Modules.Hostel.Application.Abstractions;
using UMS.Modules.Hostel.Domain.Hostels;

namespace UMS.Modules.Hostel.Application.Allocations;

/// <summary>
/// HOS-14: requirement-spec.md §2 step 10; §8 edge case "a bed frees up mid-cycle... the waitlist is
/// re-ranked and the top applicant is offered the bed before it re-enters general availability."
///
/// <para>
/// design-decisions.md "Waitlist Re-Ranking Consistency Mechanism": strictly event-driven off
/// <c>AllocationCheckedOut</c>/<c>AllocationExpired</c>, triggered only AFTER the Bed-freeing
/// transaction has fully committed - called exclusively by
/// <c>HostelWaitlistReRankingRelayWorker</c>, which polls Hostel's own outbox for these two event
/// types (never a concurrent polling sweep over Bed status directly, never inlined into the
/// freeing transaction itself).
/// </para>
/// </summary>
public sealed class WaitlistReRankingService(IHostelApplicationRepository applications, AllocationService allocationService)
{
    /// <summary>Offers the just-freed Bed's Hostel/RoomType slot to the best-ranked Waitlisted application, stopping at the first successful allocation (one Bed, one offer). A losing/ineligible candidate is left Waitlisted for the next opportunity.</summary>
    public async Task<bool> OfferToTopWaitlistedApplicantAsync(Guid hostelId, RoomType roomType, string correlationId, CancellationToken cancellationToken = default)
    {
        var candidates = await applications.GetWaitlistedCandidatesAsync(hostelId, roomType, cancellationToken).ConfigureAwait(false);

        foreach (var candidate in candidates)
        {
            var allocated = await allocationService.ApproveAndAllocateAsync(candidate.Id.Value, officerUserId: null, correlationId, cancellationToken).ConfigureAwait(false);
            if (allocated.IsSuccess)
            {
                return true;
            }
        }

        return false;
    }
}
