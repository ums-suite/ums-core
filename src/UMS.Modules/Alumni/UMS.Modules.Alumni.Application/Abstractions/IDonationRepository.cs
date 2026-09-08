using UMS.Modules.Alumni.Domain.Donations;

namespace UMS.Modules.Alumni.Application.Abstractions;

public interface IDonationRepository
{
    public Task<Donation?> GetByIdAsync(DonationId id, CancellationToken cancellationToken = default);

    public Task<Donation?> GetByInvoiceIdAsync(Guid invoiceId, CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<Donation>> ListByAlumnusAsync(Guid alumnusId, int skip, int take, CancellationToken cancellationToken = default);

    /// <summary>ALM-10: every recurring series' ROOT donation that is <c>Active</c> and due for its next cycle.</summary>
    public Task<IReadOnlyList<Donation>> ListRecurringDueAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken = default);

    public void Add(Donation donation);
}
