using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Identity.Application.Abstractions;
using UMS.Modules.Identity.Domain.Users;
using UMS.Modules.Identity.IntegrationTests.Infrastructure;

namespace UMS.Modules.Identity.IntegrationTests.Auth;

/// <summary>
/// IDN-12: password forgot/reset, exercised end-to-end over real HTTP + Postgres + Redis.
/// <see cref="Forgot_never_reveals_whether_the_identifier_resolved_to_a_real_User"/> covers the
/// real <c>POST /auth/password/forgot</c> HTTP path; the other tests seed the reset challenge
/// directly via the same DI-reach-in <c>TestDataSeeder</c> already uses (identity §2's token is
/// delivered via Notifications, not returned by this API, so an HTTP-only test has no way to learn
/// the plaintext token to present back to <c>POST /auth/password/reset</c>).
/// </summary>
[Collection(IdentityApiTestCollectionDefinition.Name)]
public class PasswordResetTests(IdentityApiFixture fixture)
{
    [Fact]
    public async Task Forgot_never_reveals_whether_the_identifier_resolved_to_a_real_User()
    {
        using var client = fixture.CreateClient();
        var user = await TestUsers.ProvisionAsync(client);

        var realResponse = await client.PostAsJsonAsync("/api/v1/identity/auth/password/forgot", new { identifier = user.Username });
        var unknownResponse = await client.PostAsJsonAsync("/api/v1/identity/auth/password/forgot", new { identifier = $"nobody-{Guid.NewGuid():N}" });

        Assert.Equal(HttpStatusCode.Accepted, realResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, unknownResponse.StatusCode);
    }

    [Fact]
    public async Task Resetting_with_a_valid_token_changes_the_password_and_the_new_password_logs_in()
    {
        using var client = fixture.CreateClient();
        var user = await TestUsers.ProvisionAsync(client);
        var plainTextToken = await SeedResetChallengeAsync(user.Id);

        var resetResponse = await client.PostAsJsonAsync("/api/v1/identity/auth/password/reset", new { token = plainTextToken, newPassword = "a-brand-new-p@ssw0rd" });
        Assert.Equal(HttpStatusCode.NoContent, resetResponse.StatusCode);

        var oldPasswordLogin = await client.PostAsJsonAsync("/api/v1/identity/auth/login", new { identifier = user.Username, password = TestUsers.DefaultPassword });
        Assert.Equal(HttpStatusCode.Unauthorized, oldPasswordLogin.StatusCode);

        var newPasswordLogin = await client.PostAsJsonAsync("/api/v1/identity/auth/login", new { identifier = user.Username, password = "a-brand-new-p@ssw0rd" });
        Assert.Equal(HttpStatusCode.OK, newPasswordLogin.StatusCode);
    }

    [Fact]
    public async Task Resetting_with_an_invalid_token_is_rejected()
    {
        using var client = fixture.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/identity/auth/password/reset", new { token = "not-a-real-token", newPassword = "a-brand-new-p@ssw0rd" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Resetting_the_password_revokes_every_existing_Session()
    {
        using var client = fixture.CreateClient();
        var user = await TestUsers.ProvisionAsync(client);
        var login = await TestUsers.LoginAsync(client, user.Username);
        var plainTextToken = await SeedResetChallengeAsync(user.Id);

        var resetResponse = await client.PostAsJsonAsync("/api/v1/identity/auth/password/reset", new { token = plainTextToken, newPassword = "a-brand-new-p@ssw0rd" });
        resetResponse.EnsureSuccessStatusCode();

        var refreshWithOldSession = await client.PostAsJsonAsync("/api/v1/identity/auth/refresh", new { refreshToken = login.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, refreshWithOldSession.StatusCode);
    }

    [Fact]
    public async Task Resetting_the_password_for_a_locked_out_account_clears_the_lockout()
    {
        using var client = fixture.CreateClient();
        var user = await TestUsers.ProvisionAsync(client);

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            await client.PostAsJsonAsync("/api/v1/identity/auth/login", new { identifier = user.Username, password = "wrong-password" });
        }

        var lockedLogin = await client.PostAsJsonAsync("/api/v1/identity/auth/login", new { identifier = user.Username, password = TestUsers.DefaultPassword });
        Assert.Equal(HttpStatusCode.Forbidden, lockedLogin.StatusCode);

        var plainTextToken = await SeedResetChallengeAsync(user.Id);
        var resetResponse = await client.PostAsJsonAsync("/api/v1/identity/auth/password/reset", new { token = plainTextToken, newPassword = "a-brand-new-p@ssw0rd" });
        resetResponse.EnsureSuccessStatusCode();

        var recoveredLogin = await client.PostAsJsonAsync("/api/v1/identity/auth/login", new { identifier = user.Username, password = "a-brand-new-p@ssw0rd" });
        Assert.Equal(HttpStatusCode.OK, recoveredLogin.StatusCode);
    }

    private async Task<string> SeedResetChallengeAsync(Guid userId)
    {
        using var scope = fixture.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var resetTokens = scope.ServiceProvider.GetRequiredService<IPasswordResetTokenService>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var user = await users.GetByIdAsync(new UserId(userId)) ?? throw new InvalidOperationException("Seeded User was not found immediately after provisioning.");
        var (plainText, hash) = resetTokens.IssueToken();
        user.IssuePasswordResetChallenge(hash, clock.UtcNow, TimeSpan.FromMinutes(30));
        await unitOfWork.SaveChangesAsync();

        return plainText;
    }
}
