namespace UMS.Modules.Identity.Application.Abstractions;

/// <summary>
/// Commits one transaction's worth of repository changes. Infrastructure's implementation also
/// drains every tracked aggregate's <c>DomainEvents</c> into the transactional outbox as part of
/// the same <c>SaveChanges</c> call (ADR-0003) - the Application layer never has to remember to do
/// that itself.
/// </summary>
public interface IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
