using UMS.Modules.Research.Domain.Publications;

namespace UMS.Modules.Research.Application.Abstractions;

public interface IPublicationRepository
{
    public Task<Publication?> GetByIdAsync(PublicationId id, CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<Publication>> ListAsync(Guid? authorFacultyMemberId, Guid? grantId, int skip, int take, CancellationToken cancellationToken = default);

    /// <summary>requirement-spec.md §2 Public Research Showcase - server-side <c>isPubliclyVisible = true</c> filtering baked into the query itself.</summary>
    public Task<IReadOnlyList<Publication>> ListPubliclyVisibleAsync(int skip, int take, CancellationToken cancellationToken = default);

    /// <summary>design-decisions.md's duplicate-detection decision: the fuzzy-match candidate pool for a DOI-absent submission - every non-merged Publication sharing the same publication year, projected down to (title, venue name) for the caller's own normalized-equality comparison.</summary>
    public Task<IReadOnlyList<Publication>> ListByPublicationYearAsync(int year, CancellationToken cancellationToken = default);

    public Task<bool> ExistsWithNormalizedDoiAsync(string normalizedDoi, CancellationToken cancellationToken = default);

    public void Add(Publication publication);
}
