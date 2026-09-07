using UMS.Modules.Admission.Domain.Applicants;

namespace UMS.Modules.Admission.Application.Abstractions;

public interface IApplicantRepository
{
    public Task<Applicant?> GetByIdAsync(ApplicantId id, CancellationToken cancellationToken = default);

    public Task<Applicant?> GetByIdentityUserIdAsync(Guid identityUserId, CancellationToken cancellationToken = default);

    public void Add(Applicant applicant);
}
