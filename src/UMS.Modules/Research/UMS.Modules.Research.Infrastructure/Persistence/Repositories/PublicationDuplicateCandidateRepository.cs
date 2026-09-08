using Microsoft.EntityFrameworkCore;
using UMS.Modules.Research.Application.Abstractions;
using UMS.Modules.Research.Domain.Publications;

namespace UMS.Modules.Research.Infrastructure.Persistence.Repositories;

internal sealed class PublicationDuplicateCandidateRepository(ResearchDbContext context) : IPublicationDuplicateCandidateRepository
{
    public Task<PublicationDuplicateCandidate?> GetByIdAsync(PublicationDuplicateCandidateId id, CancellationToken cancellationToken = default) =>
        context.PublicationDuplicateCandidates.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<IReadOnlyList<PublicationDuplicateCandidate>> ListPendingAsync(int skip, int take, CancellationToken cancellationToken = default) =>
        await context.PublicationDuplicateCandidates
            .Where(c => c.Status == PublicationDuplicateCandidateStatus.Pending)
            .OrderBy(c => c.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public void Add(PublicationDuplicateCandidate candidate) => context.PublicationDuplicateCandidates.Add(candidate);
}
