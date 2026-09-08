using UMS.Modules.Research.Domain.InstitutionalRepositoryEntries;

namespace UMS.Modules.Research.Application.Abstractions;

public interface IInstitutionalRepositoryEntryRepository
{
    public Task<InstitutionalRepositoryEntry?> GetByIdAsync(InstitutionalRepositoryEntryId id, CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<InstitutionalRepositoryEntry>> ListAsync(Guid? supervisingFacultyMemberId, int skip, int take, CancellationToken cancellationToken = default);

    /// <summary>requirement-spec.md §2 Public Research Showcase - server-side non-embargoed filtering baked into the query itself.</summary>
    public Task<IReadOnlyList<InstitutionalRepositoryEntry>> ListPublicAsync(int skip, int take, CancellationToken cancellationToken = default);

    /// <summary>design-decisions.md "InstitutionalRepositoryEntry Embargo-Lift Mechanism" - the daily worker's own lapsed-embargo sweep target set: <c>isEmbargoed = true AND embargoEndDate &lt;= today</c>.</summary>
    public Task<IReadOnlyList<InstitutionalRepositoryEntry>> ListLapsedEmbargoesAsync(DateOnly asOf, int batchSize, CancellationToken cancellationToken = default);

    public void Add(InstitutionalRepositoryEntry entry);
}
