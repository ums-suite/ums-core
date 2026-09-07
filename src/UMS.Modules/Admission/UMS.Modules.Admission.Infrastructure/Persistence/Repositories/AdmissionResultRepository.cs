using Microsoft.EntityFrameworkCore;
using UMS.Modules.Admission.Application.Abstractions;
using UMS.Modules.Admission.Domain.Results;

namespace UMS.Modules.Admission.Infrastructure.Persistence.Repositories;

internal sealed class AdmissionResultRepository(AdmissionDbContext context) : IAdmissionResultRepository
{
    public Task<AdmissionResult?> GetByIdAsync(AdmissionResultId id, CancellationToken cancellationToken = default) =>
        context.AdmissionResults.Include(r => r.Entries).FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public Task<AdmissionResult?> GetByCampaignIdAsync(Guid campaignId, CancellationToken cancellationToken = default) =>
        context.AdmissionResults.Include(r => r.Entries).FirstOrDefaultAsync(r => r.CampaignId == campaignId, cancellationToken);

    public void Add(AdmissionResult result) => context.AdmissionResults.Add(result);
}
