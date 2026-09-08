using UMS.Modules.Research.Domain.Publications;

namespace UMS.Modules.Research.Application.Abstractions;

public interface IPublicationDuplicateCandidateRepository
{
    public Task<PublicationDuplicateCandidate?> GetByIdAsync(PublicationDuplicateCandidateId id, CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<PublicationDuplicateCandidate>> ListPendingAsync(int skip, int take, CancellationToken cancellationToken = default);

    public void Add(PublicationDuplicateCandidate candidate);
}
