using UMS.Modules.Organization.Domain.Universities;

namespace UMS.Modules.Organization.Application.Abstractions;

public interface IUniversityRepository
{
    public Task<University?> GetByIdAsync(UniversityId id, CancellationToken cancellationToken = default);

    /// <summary>Row-locked read (`SELECT ... FOR UPDATE`) - design-decisions.md, "Parent-Active-Status Validation Mechanism". Held for the duration of a Campus create's parent-status check, or this University's own deactivate cascade-check.</summary>
    public Task<University?> GetByIdForUpdateAsync(UniversityId id, CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<University>> ListAsync(int skip, int take, CancellationToken cancellationToken = default);

    public Task<int> CountAsync(CancellationToken cancellationToken = default);

    public void Add(University university);
}
