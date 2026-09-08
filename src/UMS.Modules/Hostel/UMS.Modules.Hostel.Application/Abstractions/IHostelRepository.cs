namespace UMS.Modules.Hostel.Application.Abstractions;

/// <summary>
/// Uses <c>Domain.Hostels.Hostel</c> relative qualification throughout, never a bare <c>Hostel</c> -
/// this file's own namespace (<c>UMS.Modules.Hostel.Application.Abstractions</c>) is nested under
/// the module segment <c>Hostel</c> itself, so an unqualified <c>Hostel</c> binds to that namespace
/// segment (C# namespace-member lookup wins over any <c>using</c>/alias), not the aggregate type.
/// </summary>
public interface IHostelRepository
{
    public Task<Domain.Hostels.Hostel?> GetByIdAsync(Domain.Hostels.HostelId id, CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<Domain.Hostels.Hostel>> GetAllAsync(CancellationToken cancellationToken = default);

    public void Add(Domain.Hostels.Hostel hostel);
}
