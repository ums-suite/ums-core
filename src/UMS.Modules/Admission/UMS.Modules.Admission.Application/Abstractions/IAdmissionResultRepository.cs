using UMS.Modules.Admission.Domain.Results;

namespace UMS.Modules.Admission.Application.Abstractions;

public interface IAdmissionResultRepository
{
    public Task<AdmissionResult?> GetByIdAsync(AdmissionResultId id, CancellationToken cancellationToken = default);

    public Task<AdmissionResult?> GetByCampaignIdAsync(Guid campaignId, CancellationToken cancellationToken = default);

    public void Add(AdmissionResult result);
}
