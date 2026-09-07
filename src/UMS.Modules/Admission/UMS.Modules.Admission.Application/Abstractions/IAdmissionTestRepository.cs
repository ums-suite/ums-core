using UMS.Modules.Admission.Domain.Tests;

namespace UMS.Modules.Admission.Application.Abstractions;

public interface IAdmissionTestRepository
{
    public Task<AdmissionTest?> GetByIdAsync(AdmissionTestId id, CancellationToken cancellationToken = default);

    public Task<AdmissionTest?> GetByCampaignIdAsync(Guid campaignId, CancellationToken cancellationToken = default);

    public void Add(AdmissionTest test);

    /// <summary>edge-cases.md "Test-slot capacity race at admit-card generation": the atomic conditional decrement, mirroring <c>CourseOfferingRepository.TryIncrementEnrolledCountAsync</c> exactly.</summary>
    public Task<bool> TryClaimSlotSeatAsync(TestSlotId slotId, CancellationToken cancellationToken = default);
}
