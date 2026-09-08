using UMS.Modules.Hostel.Domain.Applications;
using UMS.Modules.Hostel.Domain.Hostels;

namespace UMS.Modules.Hostel.Application.Abstractions;

public interface IHostelApplicationRepository
{
    public Task<HostelApplication?> GetByIdAsync(HostelApplicationId id, CancellationToken cancellationToken = default);

    /// <summary>design-decisions.md "HostelApplication Withdrawal as a First-Class State Transition": the pessimistic lock the withdrawal command and the review-approval command both take, so they serialize.</summary>
    public Task<HostelApplication?> GetByIdForUpdateAsync(HostelApplicationId id, CancellationToken cancellationToken = default);

    public Task<HostelApplication?> GetActiveByStudentAndWindowAsync(Guid studentId, Guid applicationWindowId, CancellationToken cancellationToken = default);

    /// <summary>requirement-spec.md §4: "One active Allocation per Student" - also used at submission time to reject a second application while an existing Allocation is active anywhere (§8 edge case "409 Conflict").</summary>
    public Task<bool> StudentHasActiveAllocationAsync(Guid studentId, CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<HostelApplication>> GetByWindowAndStatusAsync(Guid applicationWindowId, HostelApplicationStatus status, CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<HostelApplication>> GetByStudentAsync(Guid studentId, CancellationToken cancellationToken = default);

    /// <summary>HOS-14: the waitlist re-ranking candidate query - every Waitlisted application (any window) whose ranked preferences include the given Hostel/RoomType, best (lowest) RankPosition first.</summary>
    public Task<IReadOnlyList<HostelApplication>> GetWaitlistedCandidatesAsync(Guid hostelId, RoomType roomType, CancellationToken cancellationToken = default);

    public void Add(HostelApplication application);
}
