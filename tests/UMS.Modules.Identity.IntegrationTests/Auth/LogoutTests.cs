using System.Net;
using System.Net.Http.Json;
using UMS.Modules.Identity.IntegrationTests.Infrastructure;

namespace UMS.Modules.Identity.IntegrationTests.Auth;

/// <summary>IDN-7/IDN-8: logout (single Session) vs. "log out everywhere" (requirement-spec.md identity §6).</summary>
[Collection(IdentityApiTestCollectionDefinition.Name)]
public class LogoutTests(IdentityApiFixture fixture)
{
    [Fact]
    public async Task Logout_revokes_only_the_current_device_session()
    {
        using var client = fixture.CreateClient();
        var user = await TestUsers.ProvisionAsync(client);
        var deviceA = await TestUsers.LoginAsync(client, user.Username);
        var deviceB = await TestUsers.LoginAsync(client, user.Username);

        var logout = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/v1/identity/auth/logout").WithBearerToken(deviceA.AccessToken));
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        var deviceARefresh = await client.PostAsJsonAsync("/api/v1/identity/auth/refresh", new { refreshToken = deviceA.RefreshToken });
        var deviceBRefresh = await client.PostAsJsonAsync("/api/v1/identity/auth/refresh", new { refreshToken = deviceB.RefreshToken });

        Assert.Equal(HttpStatusCode.Unauthorized, deviceARefresh.StatusCode);
        Assert.Equal(HttpStatusCode.OK, deviceBRefresh.StatusCode);
    }

    [Fact]
    public async Task LogoutAll_revokes_every_device_session()
    {
        using var client = fixture.CreateClient();
        var user = await TestUsers.ProvisionAsync(client);
        var deviceA = await TestUsers.LoginAsync(client, user.Username);
        var deviceB = await TestUsers.LoginAsync(client, user.Username);

        var logoutAll = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/v1/identity/auth/logout-all").WithBearerToken(deviceA.AccessToken));
        Assert.Equal(HttpStatusCode.NoContent, logoutAll.StatusCode);

        var deviceARefresh = await client.PostAsJsonAsync("/api/v1/identity/auth/refresh", new { refreshToken = deviceA.RefreshToken });
        var deviceBRefresh = await client.PostAsJsonAsync("/api/v1/identity/auth/refresh", new { refreshToken = deviceB.RefreshToken });

        Assert.Equal(HttpStatusCode.Unauthorized, deviceARefresh.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, deviceBRefresh.StatusCode);
    }

    [Fact]
    public async Task LogoutAll_also_blocks_the_very_next_permission_gated_call_on_the_revoking_devices_own_access_token()
    {
        // "Log out everywhere" is immediate at the refresh layer and, via the same live
        // sid-revocation check, blocks any request that hasn't started authorization yet too
        // (edge-cases.md, "'Log out everywhere' racing an in-flight request").
        using var client = fixture.CreateClient();
        var user = await TestUsers.ProvisionAsync(client);
        var login = await TestUsers.LoginAsync(client, user.Username);

        await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/v1/identity/auth/logout-all").WithBearerToken(login.AccessToken));

        var sessionsCall = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/api/v1/identity/sessions").WithBearerToken(login.AccessToken));

        Assert.Equal(HttpStatusCode.Forbidden, sessionsCall.StatusCode);
    }

    [Fact]
    public async Task GetSessions_lists_the_callers_own_active_sessions_and_flags_the_current_one()
    {
        using var client = fixture.CreateClient();
        var user = await TestUsers.ProvisionAsync(client);
        var login = await TestUsers.LoginAsync(client, user.Username);

        var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/api/v1/identity/sessions").WithBearerToken(login.AccessToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var sessions = await response.Content.ReadFromJsonAsync<List<SessionListItem>>();
        Assert.Contains(sessions!, s => s.Id == login.SessionId && s.IsCurrent);
    }

    private sealed record SessionListItem(Guid Id, string? UserAgent, string? CreatedFromIp, DateTimeOffset CreatedAt, DateTimeOffset LastUsedAt, string Status, bool IsCurrent);
}
