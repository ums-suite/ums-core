using Microsoft.EntityFrameworkCore;
using UMS.Modules.Admission.Application.Abstractions;
using UMS.Modules.Admission.Domain.Applicants;

namespace UMS.Modules.Admission.Infrastructure.Persistence.Repositories;

internal sealed class ApplicantRepository(AdmissionDbContext context) : IApplicantRepository
{
    public Task<Applicant?> GetByIdAsync(ApplicantId id, CancellationToken cancellationToken = default) =>
        context.Applicants.Include(a => a.AcademicHistory).FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public Task<Applicant?> GetByIdentityUserIdAsync(Guid identityUserId, CancellationToken cancellationToken = default) =>
        context.Applicants.Include(a => a.AcademicHistory).FirstOrDefaultAsync(a => a.IdentityUserId == identityUserId, cancellationToken);

    public void Add(Applicant applicant) => context.Applicants.Add(applicant);
}
