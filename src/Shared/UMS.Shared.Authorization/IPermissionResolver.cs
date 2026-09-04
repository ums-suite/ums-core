namespace UMS.Shared.Authorization;

/// <summary>
/// Resolves the last two generic steps of the platform-wide authorization validation order -
/// user status and permission - for a request that has already passed JWT authentication/token-
/// validity (the first two steps, handled by ASP.NET Core's JwtBearer middleware itself). This is
/// the "shared authorization validation pipeline" IDN-4 asks for: any module's protected endpoint
/// depends on this abstraction only, never on Identity's own internals (module-boundaries.md) -
/// Identity's own Infrastructure layer is the sole implementation, registered at the Host
/// composition root.
/// </summary>
public interface IPermissionResolver
{
    public Task<PermissionCheckOutcome> CheckAsync(
        Guid userId,
        Guid sessionId,
        string permission,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The same user-status/session-revocation steps as <see cref="CheckAsync"/>, without a
    /// specific Permission check - for an endpoint whose authorization is "you hold a live,
    /// active account and Session" plus a resource-ownership check the endpoint does itself
    /// (identity requirement-spec.md §9.4), rather than a Role-bundle Permission (e.g. a User
    /// managing their own Sessions - not a privilege anyone grants/revokes via a Role). Never
    /// returns <see cref="PermissionCheckOutcome.PermissionDenied"/>.
    /// </summary>
    public Task<PermissionCheckOutcome> CheckLivenessAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken = default);
}
