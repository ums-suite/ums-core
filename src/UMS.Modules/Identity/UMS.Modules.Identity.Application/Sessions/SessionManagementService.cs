using Microsoft.Extensions.Options;
using UMS.Modules.Identity.Application.Abstractions;
using UMS.Modules.Identity.Domain.Sessions;
using UMS.Modules.Identity.Domain.Users;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Identity.Application.Sessions;

/// <summary>
/// IDN-7/IDN-8/IDN-9: logout (single Session), "log out everywhere" (every Session for a User),
/// and Session introspection/single revoke (requirement-spec.md identity §2/§3/§6).
/// </summary>
public sealed class SessionManagementService(
    ISessionRepository sessions,
    IUnitOfWork unitOfWork,
    IAuthzCache authzCache,
    IClock clock,
    IOptions<IdentityTokenOptions> tokenOptions)
{
    public static SessionDto ToDto(Session session, Guid currentSessionId) => new(
        session.Id.Value,
        session.UserAgent,
        session.CreatedFromIp,
        session.CreatedAt,
        session.LastUsedAt,
        session.Status.ToString(),
        session.Id.Value == currentSessionId);

    public async Task<IReadOnlyList<SessionDto>> ListForUserAsync(Guid userId, Guid currentSessionId, CancellationToken cancellationToken = default)
    {
        var active = await sessions.ListActiveForUserAsync(new UserId(userId), cancellationToken).ConfigureAwait(false);
        return active.Select(s => ToDto(s, currentSessionId)).ToList();
    }

    /// <summary>IDN-7: revokes exactly the caller's own current Session (requirement-spec.md identity §6 `POST /auth/logout`).</summary>
    public Task<Result> LogoutCurrentAsync(Guid sessionId, CancellationToken cancellationToken = default) =>
        RevokeAsync(new SessionId(sessionId), ownerUserId: null, reason: "logout", cancellationToken);

    /// <summary>IDN-9: revokes one specific Session the caller owns (requirement-spec.md identity §6 `DELETE /sessions/{id}`).</summary>
    public Task<Result> RevokeOwnedSessionAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken = default) =>
        RevokeAsync(new SessionId(sessionId), ownerUserId: new UserId(userId), reason: "revoked_by_user", cancellationToken);

    /// <summary>IDN-8: "log out everywhere" - every active Session for this User (requirement-spec.md identity §2/§6 `POST /auth/logout-all`).</summary>
    public async Task<Result> LogoutAllAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var typedUserId = new UserId(userId);
        var active = await sessions.ListActiveForUserAsync(typedUserId, cancellationToken).ConfigureAwait(false);
        var now = clock.UtcNow;

        foreach (var session in active)
        {
            session.Revoke("logout_all", now);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Revocation is immediate at the refresh layer, bounded at the access-token layer (identity
        // §4) - the fast sid-revocation check closes the gap for any request that hasn't started
        // authorization yet (edge-cases.md, "'Log out everywhere' racing an in-flight request").
        foreach (var session in active)
        {
            await authzCache.RevokeSessionAsync(session.Id, tokenOptions.Value.AccessTokenLifetime, cancellationToken).ConfigureAwait(false);
        }

        return Result.Success();
    }

    private async Task<Result> RevokeAsync(SessionId sessionId, UserId? ownerUserId, string reason, CancellationToken cancellationToken)
    {
        var session = await sessions.GetByIdAsync(sessionId, cancellationToken).ConfigureAwait(false);
        if (session is null || (ownerUserId is not null && session.UserId != ownerUserId))
        {
            return Result.Failure(Error.NotFound("session.not_found", $"No Session exists with id '{sessionId.Value}'."));
        }

        session.Revoke(reason, clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await authzCache.RevokeSessionAsync(sessionId, tokenOptions.Value.AccessTokenLifetime, cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
