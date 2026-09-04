using UMS.Modules.Identity.Domain.Sessions;
using UMS.Modules.Identity.Domain.Users;

namespace UMS.Modules.Identity.UnitTests.Sessions;

/// <summary>
/// Covers requirement-spec.md identity §4 ("Refresh token rotation is single-use") and
/// edge-cases.md's "Same-device concurrent refresh (two tabs)" grace-window decision - the most
/// safety-critical logic in this flow, since a wrong call here either lets a stolen token survive
/// or logs a legitimate user out of every device on a benign race.
/// </summary>
public class SessionTests
{
    private static readonly DateTimeOffset _now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan _grace = TimeSpan.FromSeconds(5);

    private static Session OpenSession(out string initialHash)
    {
        initialHash = "hash-0";
        return Session.Open(SessionId.New(), UserId.New(), initialHash, _now.AddDays(14), userAgent: null, createdFromIp: null, _now);
    }

    [Fact]
    public void EvaluateRefreshAttempt_matching_the_current_hash_rotates_normally()
    {
        var session = OpenSession(out var currentHash);

        var decision = session.EvaluateRefreshAttempt(currentHash, _now.AddMinutes(1), _grace);

        Assert.Equal(RefreshAttemptDecision.RotateNormally, decision);
    }

    [Fact]
    public void EvaluateRefreshAttempt_reusing_a_token_superseded_within_the_grace_window_is_GraceReuse()
    {
        var session = OpenSession(out var firstHash);
        session.Rotate("hash-1", _now.AddDays(14), _now.AddSeconds(1));

        var decision = session.EvaluateRefreshAttempt(firstHash, _now.AddSeconds(3), _grace);

        Assert.Equal(RefreshAttemptDecision.GraceReuse, decision);
    }

    [Fact]
    public void EvaluateRefreshAttempt_reusing_a_token_superseded_outside_the_grace_window_is_CompromiseDetected()
    {
        var session = OpenSession(out var firstHash);
        session.Rotate("hash-1", _now.AddDays(14), _now.AddSeconds(1));

        var decision = session.EvaluateRefreshAttempt(firstHash, _now.AddSeconds(30), _grace);

        Assert.Equal(RefreshAttemptDecision.CompromiseDetected, decision);
    }

    [Fact]
    public void EvaluateRefreshAttempt_an_unrelated_token_is_CompromiseDetected()
    {
        var session = OpenSession(out _);

        var decision = session.EvaluateRefreshAttempt("never-issued-hash", _now.AddMinutes(1), _grace);

        Assert.Equal(RefreshAttemptDecision.CompromiseDetected, decision);
    }

    [Fact]
    public void EvaluateRefreshAttempt_a_second_reuse_of_the_same_stale_token_after_a_grace_rotation_is_CompromiseDetected()
    {
        // Models the bounded "exactly one grace reuse" behavior: Tab1 gets hash-0 rotated to
        // hash-1 (by Tab2's own refresh); Tab1 (holding hash-0) refreshes within grace -> allowed,
        // advancing to hash-2. A THIRD caller replaying hash-0 again must now fail outright.
        var session = OpenSession(out var hash0);
        session.Rotate("hash-1", _now.AddDays(14), _now.AddSeconds(1)); // Tab2's rotation
        var graceDecision = session.EvaluateRefreshAttempt(hash0, _now.AddSeconds(2), _grace);
        Assert.Equal(RefreshAttemptDecision.GraceReuse, graceDecision);
        session.Rotate("hash-2", _now.AddDays(14), _now.AddSeconds(2)); // Tab1's grace-allowed rotation

        var secondReplay = session.EvaluateRefreshAttempt(hash0, _now.AddSeconds(3), _grace);

        Assert.Equal(RefreshAttemptDecision.CompromiseDetected, secondReplay);
    }

    [Fact]
    public void EvaluateRefreshAttempt_after_expiry_is_Rejected()
    {
        var session = OpenSession(out var hash);

        var decision = session.EvaluateRefreshAttempt(hash, _now.AddDays(15), _grace);

        Assert.Equal(RefreshAttemptDecision.Rejected, decision);
    }

    [Fact]
    public void EvaluateRefreshAttempt_after_revocation_is_Rejected_even_for_the_current_token()
    {
        var session = OpenSession(out var hash);
        session.Revoke("test", _now.AddMinutes(1));

        var decision = session.EvaluateRefreshAttempt(hash, _now.AddMinutes(2), _grace);

        Assert.Equal(RefreshAttemptDecision.Rejected, decision);
    }

    [Fact]
    public void Revoke_raises_SessionRevoked_exactly_once_even_if_called_twice()
    {
        var session = OpenSession(out _);
        session.ClearDomainEvents();

        session.Revoke("first", _now);
        session.Revoke("second", _now.AddMinutes(1));

        Assert.Single(session.DomainEvents);
        Assert.Equal(SessionStatus.Revoked, session.Status);
        Assert.Equal("first", session.RevokedReason);
    }

    [Fact]
    public void Two_independent_Sessions_for_the_same_user_do_not_interact_on_refresh()
    {
        // "Concurrent refresh from two devices" (edge-cases.md) - each device's own Session (its
        // own rotation chain) is unaffected by the other device's rotation.
        var userId = UserId.New();
        var deviceA = Session.Open(SessionId.New(), userId, "a-hash-0", _now.AddDays(14), "device-a", null, _now);
        var deviceB = Session.Open(SessionId.New(), userId, "b-hash-0", _now.AddDays(14), "device-b", null, _now);

        deviceA.Rotate("a-hash-1", _now.AddDays(14), _now.AddSeconds(1));
        var deviceBDecision = deviceB.EvaluateRefreshAttempt("b-hash-0", _now.AddSeconds(2), _grace);

        Assert.Equal(RefreshAttemptDecision.RotateNormally, deviceBDecision);
    }
}
