using Microsoft.EntityFrameworkCore;
using UMS.Modules.Research.Application.Abstractions;
using UMS.Modules.Research.Domain.InstitutionalRepositoryEntries;

namespace UMS.Modules.Research.Infrastructure.Persistence.Repositories;

internal sealed class InstitutionalRepositoryEntryRepository(ResearchDbContext context) : IInstitutionalRepositoryEntryRepository
{
    public Task<InstitutionalRepositoryEntry?> GetByIdAsync(InstitutionalRepositoryEntryId id, CancellationToken cancellationToken = default) =>
        context.InstitutionalRepositoryEntries.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public async Task<IReadOnlyList<InstitutionalRepositoryEntry>> ListAsync(Guid? supervisingFacultyMemberId, int skip, int take, CancellationToken cancellationToken = default)
    {
        var query = context.InstitutionalRepositoryEntries.AsQueryable();

        if (supervisingFacultyMemberId is { } supervisorId)
        {
            query = query.Where(e => e.SupervisingFacultyMemberId == supervisorId);
        }

        return await query
            .OrderByDescending(e => e.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>requirement-spec.md §2/§4: excluded from every anonymous/public read regardless of AccessLevel while embargoed.</summary>
    public async Task<IReadOnlyList<InstitutionalRepositoryEntry>> ListPublicAsync(int skip, int take, CancellationToken cancellationToken = default) =>
        await context.InstitutionalRepositoryEntries
            .Where(e => !e.Embargo.IsEmbargoed)
            .OrderByDescending(e => e.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<InstitutionalRepositoryEntry>> ListLapsedEmbargoesAsync(DateOnly asOf, int batchSize, CancellationToken cancellationToken = default) =>
        await context.InstitutionalRepositoryEntries
            .Where(e => e.Embargo.IsEmbargoed && e.Embargo.EmbargoEndDate != null && e.Embargo.EmbargoEndDate <= asOf)
            .Take(batchSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public void Add(InstitutionalRepositoryEntry entry) => context.InstitutionalRepositoryEntries.Add(entry);
}
