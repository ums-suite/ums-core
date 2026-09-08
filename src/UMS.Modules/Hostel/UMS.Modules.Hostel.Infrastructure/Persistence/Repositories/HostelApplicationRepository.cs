using Microsoft.EntityFrameworkCore;
using UMS.Modules.Hostel.Application.Abstractions;
using UMS.Modules.Hostel.Domain.Allocations;
using UMS.Modules.Hostel.Domain.Applications;
using UMS.Modules.Hostel.Domain.Hostels;

namespace UMS.Modules.Hostel.Infrastructure.Persistence.Repositories;

internal sealed class HostelApplicationRepository(HostelDbContext context) : IHostelApplicationRepository
{
    private static readonly HostelApplicationStatus[] OpenStatuses =
    [
        HostelApplicationStatus.Draft,
        HostelApplicationStatus.Submitted,
        HostelApplicationStatus.Ranked,
        HostelApplicationStatus.Waitlisted,
        HostelApplicationStatus.Approved,
    ];

    private static readonly AllocationStatus[] NonTerminalAllocationStatuses =
    [
        AllocationStatus.Pending,
        AllocationStatus.FeePaid,
        AllocationStatus.Active,
    ];

    public Task<HostelApplication?> GetByIdAsync(HostelApplicationId id, CancellationToken cancellationToken = default) =>
        Query().FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    /// <summary>
    /// design-decisions.md "HostelApplication Withdrawal as a First-Class State Transition": two
    /// steps, not one - a raw <c>SELECT ... FOR UPDATE</c> to take the lock (its own result set
    /// discarded), followed by a normal tracked LINQ query with <c>.Include</c> for the owned
    /// <c>Preferences</c> collection - both run inside the SAME caller-managed transaction, so the
    /// second query sees its own already-locked row. Mirrors Finance's own
    /// <c>PaymentRepository.GetByIdForUpdateAsync</c> exactly (composing <c>.Include</c> directly on
    /// top of a raw <c>FromSqlInterpolated</c> root is the same known EF Core limitation).
    /// </summary>
    public async Task<HostelApplication?> GetByIdForUpdateAsync(HostelApplicationId id, CancellationToken cancellationToken = default)
    {
        await context.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM hostel.hostel_applications WHERE id = {id.Value} FOR UPDATE", cancellationToken).ConfigureAwait(false);
        return await Query().FirstOrDefaultAsync(a => a.Id == id, cancellationToken).ConfigureAwait(false);
    }

    public Task<HostelApplication?> GetActiveByStudentAndWindowAsync(Guid studentId, Guid applicationWindowId, CancellationToken cancellationToken = default) =>
        Query().FirstOrDefaultAsync(a => a.StudentId == studentId && a.ApplicationWindowId == applicationWindowId && OpenStatuses.Contains(a.Status), cancellationToken);

    public Task<bool> StudentHasActiveAllocationAsync(Guid studentId, CancellationToken cancellationToken = default) =>
        context.Allocations.AnyAsync(a => a.StudentId == studentId && NonTerminalAllocationStatuses.Contains(a.Status), cancellationToken);

    public async Task<IReadOnlyList<HostelApplication>> GetByWindowAndStatusAsync(Guid applicationWindowId, HostelApplicationStatus status, CancellationToken cancellationToken = default) =>
        await Query().Where(a => a.ApplicationWindowId == applicationWindowId && a.Status == status).ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<HostelApplication>> GetByStudentAsync(Guid studentId, CancellationToken cancellationToken = default) =>
        await Query().Where(a => a.StudentId == studentId).OrderByDescending(a => a.CreatedAt).ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<HostelApplication>> GetWaitlistedCandidatesAsync(Guid hostelId, RoomType roomType, CancellationToken cancellationToken = default) =>
        await Query()
            .Where(a => a.Status == HostelApplicationStatus.Waitlisted && a.Preferences.Any(p => p.HostelId == hostelId && p.PreferredRoomType == roomType))
            .OrderBy(a => a.RankPosition ?? int.MaxValue)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public void Add(HostelApplication application) => context.HostelApplications.Add(application);

    private IQueryable<HostelApplication> Query() => context.HostelApplications.Include(a => a.Preferences);
}
