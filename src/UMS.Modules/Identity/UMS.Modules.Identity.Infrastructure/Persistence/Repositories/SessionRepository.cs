using Microsoft.EntityFrameworkCore;
using UMS.Modules.Identity.Application.Abstractions;
using UMS.Modules.Identity.Domain.Sessions;
using UMS.Modules.Identity.Domain.Users;

namespace UMS.Modules.Identity.Infrastructure.Persistence.Repositories;

internal sealed class SessionRepository(IdentityDbContext context) : ISessionRepository
{
    public Task<Session?> GetByIdAsync(SessionId id, CancellationToken cancellationToken = default) =>
        context.Sessions.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Session>> ListActiveForUserAsync(UserId userId, CancellationToken cancellationToken = default) =>
        await context.Sessions
            .Where(s => s.UserId == userId && s.Status == SessionStatus.Active)
            .OrderByDescending(s => s.LastUsedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public void Add(Session session) => context.Sessions.Add(session);
}
