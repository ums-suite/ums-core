namespace UMS.Shared.Authorization;

/// <summary>
/// The outcome of resolving "does this User currently hold this Permission, at this scope"
/// (requirement-spec.md identity §2, ADR-0006) - deliberately more granular than a bare
/// bool so <see cref="PermissionAuthorizationHandler"/> can log/observe exactly which of the
/// platform-wide validation-order steps (identity §2: "authentication -> token validity -> user
/// status -> permission -> resource ownership -> organizational scope") failed, even though every
/// non-<see cref="Granted"/> outcome results in the same 403 to the caller.
/// </summary>
public enum PermissionCheckOutcome
{
    Granted,
    UserNotFound,
    UserInactive,
    SessionRevoked,
    PermissionDenied,
}
