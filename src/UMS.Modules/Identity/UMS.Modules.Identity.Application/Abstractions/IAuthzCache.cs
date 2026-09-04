using UMS.Modules.Identity.Domain.Sessions;
using UMS.Modules.Identity.Domain.Users;

namespace UMS.Modules.Identity.Application.Abstractions;

/// <summary>
/// Write side of the shared permission-check cache <see cref="UMS.Shared.Authorization.IPermissionResolver"/>
/// reads (design-decisions.md, "Permission-Check Caching vs. Live Lookup"). Every application
/// service that raises <c>RoleAssigned</c>/<c>RoleRevoked</c>/<c>UserStatusChanged</c> calls
/// <see cref="InvalidateUserAsync"/> synchronously in the same request, and every Session-revoking
/// service calls <see cref="RevokeSessionAsync"/> - "invalidated immediately and synchronously
/// whenever ... fires", not left to TTL expiry alone.
/// </summary>
public interface IAuthzCache
{
    public Task InvalidateUserAsync(UserId userId, CancellationToken cancellationToken = default);

    public Task RevokeSessionAsync(SessionId sessionId, TimeSpan ttl, CancellationToken cancellationToken = default);
}
