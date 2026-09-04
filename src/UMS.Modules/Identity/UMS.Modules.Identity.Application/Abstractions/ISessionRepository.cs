using UMS.Modules.Identity.Domain.Sessions;
using UMS.Modules.Identity.Domain.Users;

namespace UMS.Modules.Identity.Application.Abstractions;

public interface ISessionRepository
{
    public Task<Session?> GetByIdAsync(SessionId id, CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<Session>> ListActiveForUserAsync(UserId userId, CancellationToken cancellationToken = default);

    public void Add(Session session);
}
