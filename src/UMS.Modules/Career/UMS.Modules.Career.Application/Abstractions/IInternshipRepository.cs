using UMS.Modules.Career.Domain.Internships;

namespace UMS.Modules.Career.Application.Abstractions;

public interface IInternshipRepository
{
    public Task<Internship?> GetByIdAsync(InternshipId id, CancellationToken cancellationToken = default);

    /// <summary>CAR-4: browse/filter (requirement-spec.md §2.2) - only `Published`/`ApplicationsOpen` postings are returned unless `includeAllStatuses` (staff view).</summary>
    public Task<IReadOnlyList<Internship>> ListAsync(Guid? programId, Guid? employerProfileId, string? keyword, bool includeAllStatuses, int skip, int take, CancellationToken cancellationToken = default);

    /// <summary>CAR-3: the scheduled deadline sweep's own batch read.</summary>
    public Task<IReadOnlyList<Internship>> ListDueForClosureAsync(DateTimeOffset asOf, int batchSize, CancellationToken cancellationToken = default);

    public void Add(Internship internship);
}
