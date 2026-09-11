using Microsoft.EntityFrameworkCore;
using UMS.Modules.Career.Application.Abstractions;
using UMS.Modules.Career.Domain.Internships;

namespace UMS.Modules.Career.Infrastructure.Persistence.Repositories;

internal sealed class InternshipRepository(CareerDbContext context) : IInternshipRepository
{
    public Task<Internship?> GetByIdAsync(InternshipId id, CancellationToken cancellationToken = default) =>
        context.Internships.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Internship>> ListAsync(Guid? programId, Guid? employerProfileId, string? keyword, bool includeAllStatuses, int skip, int take, CancellationToken cancellationToken = default)
    {
        var query = context.Internships.AsQueryable();

        if (!includeAllStatuses)
        {
            query = query.Where(i => i.Status == InternshipStatus.Published || i.Status == InternshipStatus.ApplicationsOpen);
        }

        if (employerProfileId is { } employer)
        {
            query = query.Where(i => i.EmployerProfileId == employer);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(i => EF.Functions.ILike(i.Title, $"%{keyword}%") || EF.Functions.ILike(i.Description, $"%{keyword}%"));
        }

        var items = await query.OrderByDescending(i => i.CreatedAt).Skip(skip).Take(take).ToListAsync(cancellationToken).ConfigureAwait(false);

        // requirement-spec.md §2.2: eligibility criteria are shown as informational filters, not a
        // hard list-time gate - applied in-memory (EF.Property array-contains translation for a
        // uuid[] column is unreliable across providers, and this is an informational filter only).
        if (programId is { } program)
        {
            items = items.Where(i => i.EligibilityProgramIds.Count == 0 || i.EligibilityProgramIds.Contains(program)).ToList();
        }

        return items;
    }

    public async Task<IReadOnlyList<Internship>> ListDueForClosureAsync(DateTimeOffset asOf, int batchSize, CancellationToken cancellationToken = default) =>
        await context.Internships
            .Where(i => i.Status == InternshipStatus.ApplicationsOpen && i.ApplicationDeadline <= asOf)
            .OrderBy(i => i.ApplicationDeadline)
            .Take(batchSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public void Add(Internship internship) => context.Internships.Add(internship);
}
