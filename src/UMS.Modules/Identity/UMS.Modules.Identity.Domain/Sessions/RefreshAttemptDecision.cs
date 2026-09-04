namespace UMS.Modules.Identity.Domain.Sessions;

/// <summary>
/// The result of evaluating a presented refresh token against a <see cref="Session"/>'s current
/// rotation state (requirement-spec.md identity §4 "Refresh token rotation is single-use";
/// edge-cases.md "Same-device concurrent refresh (two tabs)").
/// </summary>
public enum RefreshAttemptDecision
{
    /// <summary>Presented token matches the current one - rotate normally.</summary>
    RotateNormally,

    /// <summary>
    /// Presented token matches the immediately-preceding one, superseded within the grace window -
    /// a benign same-device race (edge-cases.md), not compromise. Rotate again rather than revoke.
    /// </summary>
    GraceReuse,

    /// <summary>Presented token matches neither the current nor a still-in-grace previous one - treated as a compromise signal (identity §4).</summary>
    CompromiseDetected,

    /// <summary>The Session is already revoked or its current refresh token has expired.</summary>
    Rejected,
}
