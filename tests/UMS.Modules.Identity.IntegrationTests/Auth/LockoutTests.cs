using System.Net;
using System.Net.Http.Json;
using UMS.Modules.Identity.IntegrationTests.Infrastructure;

namespace UMS.Modules.Identity.IntegrationTests.Auth;

/// <summary>IDN-17: failed-login throttling and account lockout, exercised end-to-end over real HTTP + Postgres + Redis (this suite's own `Identity:Lockout:MaxFailedAttempts` is 3 - see IdentityApiFixture).</summary>
[Collection(IdentityApiTestCollectionDefinition.Name)]
public class LockoutTests(IdentityApiFixture fixture)
{
    [Fact]
    public async Task Crossing_the_failed_attempt_threshold_locks_the_account()
    {
        using var client = fixture.CreateClient();
        var user = await TestUsers.ProvisionAsync(client);

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            var response = await client.PostAsJsonAsync("/api/v1/identity/auth/login", new { identifier = user.Username, password = "wrong-password" });
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        // The 4th attempt, even with the CORRECT password, is now rejected as locked - identity §8
        // "Password reset requested for a locked-out account" only makes sense if a plain retry of
        // the real password does not itself un-stick the account.
        var lockedResponse = await client.PostAsJsonAsync("/api/v1/identity/auth/login", new { identifier = user.Username, password = TestUsers.DefaultPassword });

        Assert.Equal(HttpStatusCode.Forbidden, lockedResponse.StatusCode);
        var body = await lockedResponse.Content.ReadAsStringAsync();
        Assert.Contains("auth.account_locked", body);
    }

    [Fact]
    public async Task A_successful_login_before_the_threshold_resets_the_failure_counter()
    {
        using var client = fixture.CreateClient();
        var user = await TestUsers.ProvisionAsync(client);

        await client.PostAsJsonAsync("/api/v1/identity/auth/login", new { identifier = user.Username, password = "wrong-password" });
        await client.PostAsJsonAsync("/api/v1/identity/auth/login", new { identifier = user.Username, password = "wrong-password" });

        var successfulLogin = await client.PostAsJsonAsync("/api/v1/identity/auth/login", new { identifier = user.Username, password = TestUsers.DefaultPassword });
        Assert.Equal(HttpStatusCode.OK, successfulLogin.StatusCode);

        // Two more failures after the reset - still below the threshold of 3 fresh failures.
        await client.PostAsJsonAsync("/api/v1/identity/auth/login", new { identifier = user.Username, password = "wrong-password" });
        var stillUnlockedAttempt = await client.PostAsJsonAsync("/api/v1/identity/auth/login", new { identifier = user.Username, password = "wrong-password" });

        Assert.Equal(HttpStatusCode.Unauthorized, stillUnlockedAttempt.StatusCode);
    }
}
