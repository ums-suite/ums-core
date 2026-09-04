using UMS.Modules.Identity.Domain.Users;
using UMS.Shared.Domain;

namespace UMS.Modules.Identity.Application.Abstractions;

/// <summary>Persistence port for <see cref="User"/> - the Application layer never references EF Core directly (ums-conventions.md, module isolation).</summary>
public interface IUserRepository
{
    public Task<User?> GetByIdAsync(UserId id, CancellationToken cancellationToken = default);

    /// <summary>Resolves username, email, mobile, or university id - exactly one <see cref="User"/> or none (requirement-spec.md identity §2, login identifier resolution).</summary>
    public Task<User?> GetByIdentifierAsync(string identifier, CancellationToken cancellationToken = default);

    public Task<User?> GetByEmailAsync(Email email, CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<User>> ListAsync(int skip, int take, CancellationToken cancellationToken = default);

    public Task<int> CountAsync(CancellationToken cancellationToken = default);

    public void Add(User user);
}
