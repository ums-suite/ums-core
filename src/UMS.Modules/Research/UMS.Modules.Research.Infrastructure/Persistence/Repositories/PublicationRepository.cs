using Microsoft.EntityFrameworkCore;
using UMS.Modules.Research.Application.Abstractions;
using UMS.Modules.Research.Domain.Publications;

namespace UMS.Modules.Research.Infrastructure.Persistence.Repositories;

internal sealed class PublicationRepository(ResearchDbContext context) : IPublicationRepository
{
    public Task<Publication?> GetByIdAsync(PublicationId id, CancellationToken cancellationToken = default) =>
        context.Publications.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Publication>> ListAsync(Guid? authorFacultyMemberId, Guid? grantId, int skip, int take, CancellationToken cancellationToken = default)
    {
        var query = context.Publications.Where(p => p.MergedIntoPublicationId == null);

        if (authorFacultyMemberId is { } fmId)
        {
            query = query.Where(p => p.Authors.Any(a => a.FacultyMemberId == fmId));
        }

        if (grantId is { } gid)
        {
            query = query.Where(p => p.FundedByGrantIds.Contains(gid));
        }

        return await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Publication>> ListPubliclyVisibleAsync(int skip, int take, CancellationToken cancellationToken = default) =>
        await context.Publications
            .Where(p => p.IsPubliclyVisible && p.MergedIntoPublicationId == null)
            .OrderByDescending(p => p.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<Publication>> ListByPublicationYearAsync(int year, CancellationToken cancellationToken = default) =>
        await context.Publications
            .Where(p => p.MergedIntoPublicationId == null && p.Citation.PublicationDate.Year == year)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <summary>Queries the real, mapped <c>normalized_doi</c> generated column (see <c>PublicationConfiguration</c>'s own remarks) - never <see cref="CitationMetadata.NormalizedDoi"/> directly, which is EF-<c>Ignore</c>d and cannot be translated to SQL.</summary>
    public Task<bool> ExistsWithNormalizedDoiAsync(string normalizedDoi, CancellationToken cancellationToken = default) =>
        context.Publications.AnyAsync(p => EF.Property<string?>(p, "NormalizedDoi") == normalizedDoi, cancellationToken);

    public void Add(Publication publication) => context.Publications.Add(publication);
}
