using Microsoft.EntityFrameworkCore;
using UMS.Modules.Alumni.Application.Abstractions;
using UMS.Modules.Alumni.Domain.Donations;

namespace UMS.Modules.Alumni.Infrastructure.Persistence.Repositories;

internal sealed class DonationRepository(AlumniDbContext context) : IDonationRepository
{
    public Task<Donation?> GetByIdAsync(DonationId id, CancellationToken cancellationToken = default) =>
        context.Donations.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    public Task<Donation?> GetByInvoiceIdAsync(Guid invoiceId, CancellationToken cancellationToken = default) =>
        context.Donations.FirstOrDefaultAsync(d => d.InvoiceId == invoiceId, cancellationToken);

    public async Task<IReadOnlyList<Donation>> ListByAlumnusAsync(Guid alumnusId, int skip, int take, CancellationToken cancellationToken = default) =>
        await context.Donations
            .Where(d => d.AlumnusId == alumnusId)
            .OrderByDescending(d => d.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<Donation>> ListRecurringDueAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken = default) =>
        await context.Donations
            .Where(d => d.SeriesRootDonationId == null && d.RecurrenceStatus == RecurrenceStatus.Active && d.NextChargeAt != null && d.NextChargeAt <= now)
            .OrderBy(d => d.NextChargeAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public void Add(Donation donation) => context.Donations.Add(donation);
}
