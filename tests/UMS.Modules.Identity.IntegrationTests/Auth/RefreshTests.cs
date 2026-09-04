using System.Net;
using System.Net.Http.Json;
using UMS.Modules.Identity.Application.Auth;
using UMS.Modules.Identity.IntegrationTests.Infrastructure;

namespace UMS.Modules.Identity.IntegrationTests.Auth;

/// <summary>IDN-6, edge-cases.md's refresh-rotation edge cases, exercised end-to-end over real HTTP + Postgres + Redis.</summary>
[Collection(IdentityApiTestCollectionDefinition.Name)]
public class RefreshTests(IdentityApiFixture fixture)
{
    [Fact]
    public async Task Refresh_with_the_current_token_returns_a_new_pair()
    {
        using var client = fixture.CreateClient();
        var user = await TestUsers.ProvisionAsync(client);
        var login = await TestUsers.LoginAsync(client, user.Username);

        var response = await client.PostAsJsonAsync("/api/v1/identity/auth/refresh", new { refreshToken = login.RefreshToken });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var rotated = await response.Content.ReadFromJsonAsync<TokenPairResult>();
        Assert.NotEqual(login.RefreshToken, rotated!.RefreshToken);
        Assert.Equal(login.SessionId, rotated.SessionId);
    }

    [Fact]
    public async Task Reusing_an_already_rotated_refresh_token_is_rejected_as_compromise()
    {
        // requirement-spec.md identity §4: "Refresh token rotation is single-use."
        using var client = fixture.CreateClient();
        var user = await TestUsers.ProvisionAsync(client);
        var login = await TestUsers.LoginAsync(client, user.Username);

        var firstRefresh = await client.PostAsJsonAsync("/api/v1/identity/auth/refresh", new { refreshToken = login.RefreshToken });
        firstRefresh.EnsureSuccessStatusCode();

        // Long enough after the first rotation to be outside the grace window - a genuine replay.
        await Task.Delay(TimeSpan.FromSeconds(4));
        var reuseResponse = await client.PostAsJsonAsync("/api/v1/identity/auth/refresh", new { refreshToken = login.RefreshToken });

        Assert.Equal(HttpStatusCode.Unauthorized, reuseResponse.StatusCode);
    }

    [Fact]
    public async Task After_reuse_is_detected_the_rotated_replacement_token_is_also_rejected()
    {
        // The whole Session (this device's chain) is revoked, not just the reused token.
        using var client = fixture.CreateClient();
        var user = await TestUsers.ProvisionAsync(client);
        var login = await TestUsers.LoginAsync(client, user.Username);

        var firstRefreshResponse = await client.PostAsJsonAsync("/api/v1/identity/auth/refresh", new { refreshToken = login.RefreshToken });
        var rotated = await firstRefreshResponse.Content.ReadFromJsonAsync<TokenPairResult>();

        await Task.Delay(TimeSpan.FromSeconds(4));
        await client.PostAsJsonAsync("/api/v1/identity/auth/refresh", new { refreshToken = login.RefreshToken }); // triggers compromise + revoke

        var rotatedTokenAttempt = await client.PostAsJsonAsync("/api/v1/identity/auth/refresh", new { refreshToken = rotated!.RefreshToken });

        Assert.Equal(HttpStatusCode.Unauthorized, rotatedTokenAttempt.StatusCode);
    }

    [Fact]
    public async Task Refreshing_the_same_just_rotated_token_within_the_grace_window_does_not_revoke_the_session()
    {
        // edge-cases.md, "Same-device concurrent refresh (two tabs)".
        using var client = fixture.CreateClient();
        var user = await TestUsers.ProvisionAsync(client);
        var login = await TestUsers.LoginAsync(client, user.Username);

        var firstRefresh = await client.PostAsJsonAsync("/api/v1/identity/auth/refresh", new { refreshToken = login.RefreshToken });
        firstRefresh.EnsureSuccessStatusCode();

        // Immediately (well within the 3s grace window configured for this suite) replay the
        // original token, simulating a second tab that hadn't yet seen the BFF's rotated value.
        var graceReuse = await client.PostAsJsonAsync("/api/v1/identity/auth/refresh", new { refreshToken = login.RefreshToken });

        Assert.Equal(HttpStatusCode.OK, graceReuse.StatusCode);
    }

    [Fact]
    public async Task Two_devices_logged_in_as_the_same_user_refresh_independently()
    {
        // edge-cases.md, "Concurrent refresh from two devices" - each device's own Session.
        using var client = fixture.CreateClient();
        var user = await TestUsers.ProvisionAsync(client);

        var deviceA = await TestUsers.LoginAsync(client, user.Username);
        var deviceB = await TestUsers.LoginAsync(client, user.Username);
        Assert.NotEqual(deviceA.SessionId, deviceB.SessionId);

        var (refreshA, refreshB) = (
            await client.PostAsJsonAsync("/api/v1/identity/auth/refresh", new { refreshToken = deviceA.RefreshToken }),
            await client.PostAsJsonAsync("/api/v1/identity/auth/refresh", new { refreshToken = deviceB.RefreshToken }));

        Assert.Equal(HttpStatusCode.OK, refreshA.StatusCode);
        Assert.Equal(HttpStatusCode.OK, refreshB.StatusCode);
    }

    [Fact]
    public async Task Refresh_with_a_malformed_token_returns_401()
    {
        using var client = fixture.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/identity/auth/refresh", new { refreshToken = "not-a-real-token" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
