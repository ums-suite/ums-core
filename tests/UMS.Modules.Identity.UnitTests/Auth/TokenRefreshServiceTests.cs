using Microsoft.Extensions.Options;
using UMS.Modules.Identity.Application;
using UMS.Modules.Identity.Application.Auth;
using UMS.Modules.Identity.Domain.Sessions;
using UMS.Modules.Identity.Domain.Users;
using UMS.Modules.Identity.UnitTests.TestDoubles;
using UMS.Shared.Domain;

namespace UMS.Modules.Identity.UnitTests.Auth;

public class TokenRefreshServiceTests
{
    private static readonly DateTimeOffset _now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static TokenRefreshService BuildService(
        FakeSessionRepository sessions,
        FakeUserRepository users,
        FakeAuthzCache authzCache,
        FakeClock clock,
        FakeTokenService tokenService) =>
        new(sessions, users, new FakeRoleRepository(), tokenService, new FakeUnitOfWork(), authzCache, clock, Options.Create(new IdentityTokenOptions { RefreshReuseGraceWindow = TimeSpan.FromSeconds(5) }));

    private static (User User, Session Session, string RefreshToken) SeedLoggedInUser(FakeUserRepository users, FakeSessionRepository sessions, FakeTokenService tokenService)
    {
        var credential = Credential.FromHash("hash", "fake", _now);
        var user = User.Provision("jane.doe", Email.Create("jane.doe@example.edu.bd").Value, PersonName.Create("Jane", "Doe").Value, null, null, credential, _now);
        users.Users.Add(user);

        var sessionId = SessionId.New();
        var refreshToken = tokenService.IssueRefreshToken(sessionId, _now);
        var session = Session.Open(sessionId, user.Id, refreshToken.Hash, refreshToken.ExpiresAt, null, null, _now);
        sessions.Sessions.Add(session);

        return (user, session, refreshToken.PlaintextValue);
    }

    [Fact]
    public async Task RefreshAsync_with_the_current_token_rotates_and_returns_a_new_pair()
    {
        var users = new FakeUserRepository();
        var sessions = new FakeSessionRepository();
        var tokenService = new FakeTokenService();
        var (_, session, refreshToken) = SeedLoggedInUser(users, sessions, tokenService);
        var service = BuildService(sessions, users, new FakeAuthzCache(), new FakeClock(_now.AddMinutes(1)), tokenService);

        var result = await service.RefreshAsync(new RefreshRequest(refreshToken, null, null));

        Assert.True(result.IsSuccess);
        Assert.NotEqual(refreshToken, result.Value.RefreshToken);
        Assert.Equal(SessionStatus.Active, session.Status);
    }

    [Fact]
    public async Task RefreshAsync_reusing_an_already_rotated_token_revokes_the_Session_and_the_authz_cache()
    {
        var users = new FakeUserRepository();
        var sessions = new FakeSessionRepository();
        var tokenService = new FakeTokenService();
        var (_, session, firstRefreshToken) = SeedLoggedInUser(users, sessions, tokenService);
        var authzCache = new FakeAuthzCache();
        var clock = new FakeClock(_now.AddSeconds(1));
        var service = BuildService(sessions, users, authzCache, clock, tokenService);

        // First refresh rotates hash-0 -> hash-1.
        await service.RefreshAsync(new RefreshRequest(firstRefreshToken, null, null));

        // A long-delayed replay of the now-doubly-stale original token is compromise, not grace.
        clock.UtcNow = _now.AddMinutes(5);
        var reuseResult = await service.RefreshAsync(new RefreshRequest(firstRefreshToken, null, null));

        Assert.False(reuseResult.IsSuccess);
        Assert.Equal("auth.refresh_reuse_detected", reuseResult.Error!.Code);
        Assert.Equal(SessionStatus.Revoked, session.Status);
        Assert.Contains(session.Id, authzCache.RevokedSessions);
    }

    [Fact]
    public async Task RefreshAsync_within_the_grace_window_does_not_revoke_the_Session()
    {
        // Edge-cases.md, "Same-device concurrent refresh (two tabs)".
        var users = new FakeUserRepository();
        var sessions = new FakeSessionRepository();
        var tokenService = new FakeTokenService();
        var (_, session, firstRefreshToken) = SeedLoggedInUser(users, sessions, tokenService);
        var clock = new FakeClock(_now.AddSeconds(1));
        var service = BuildService(sessions, users, new FakeAuthzCache(), clock, tokenService);

        await service.RefreshAsync(new RefreshRequest(firstRefreshToken, null, null));

        clock.UtcNow = _now.AddSeconds(3);
        var graceResult = await service.RefreshAsync(new RefreshRequest(firstRefreshToken, null, null));

        Assert.True(graceResult.IsSuccess);
        Assert.Equal(SessionStatus.Active, session.Status);
    }

    [Fact]
    public async Task RefreshAsync_with_a_malformed_token_fails_without_touching_the_Session_store()
    {
        var service = BuildService(new FakeSessionRepository(), new FakeUserRepository(), new FakeAuthzCache(), new FakeClock(_now), new FakeTokenService());

        var result = await service.RefreshAsync(new RefreshRequest("not-a-valid-token", null, null));

        Assert.False(result.IsSuccess);
        Assert.Equal("auth.invalid_refresh_token", result.Error!.Code);
    }
}
