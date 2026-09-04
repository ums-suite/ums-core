using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using OtpNet;
using UMS.Modules.Identity.Application.Abstractions;
using UMS.Modules.Identity.Application.Auth;
using UMS.Modules.Identity.Domain.Roles;
using UMS.Modules.Identity.Domain.Users;
using UMS.Modules.Identity.IntegrationTests.Infrastructure;

namespace UMS.Modules.Identity.IntegrationTests.Auth;

/// <summary>IDN-10/IDN-11/IDN-16: TOTP MFA enrollment/verification and the MFA-required-role activation gate, exercised end-to-end over real HTTP + Postgres + Redis, with real RFC 6238 codes computed from the enrollment response's own secret.</summary>
[Collection(IdentityApiTestCollectionDefinition.Name)]
public class MfaTests(IdentityApiFixture fixture)
{
    [Fact]
    public async Task Enrolling_MFA_returns_a_secret_and_an_otp_auth_uri()
    {
        using var client = fixture.CreateClient();
        var user = await TestUsers.ProvisionAsync(client);
        var login = await TestUsers.LoginAsync(client, user.Username);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/identity/auth/mfa/enroll").WithBearerToken(login.AccessToken);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var enrollment = await response.Content.ReadFromJsonAsync<MfaEnrollmentDto>();
        Assert.False(string.IsNullOrWhiteSpace(enrollment!.SecretBase32));
        Assert.Contains("otpauth://totp/", enrollment.OtpAuthUri);
    }

    [Fact]
    public async Task Verifying_enrollment_with_the_correct_code_completes_it()
    {
        using var client = fixture.CreateClient();
        var user = await TestUsers.ProvisionAsync(client);
        var login = await TestUsers.LoginAsync(client, user.Username);
        var enrollment = await EnrollAsync(client, login.AccessToken);

        var verifyRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/identity/auth/mfa/verify")
        {
            Content = JsonContent.Create(new { code = ComputeValidCode(enrollment.SecretBase32) }),
        }.WithBearerToken(login.AccessToken);
        var verifyResponse = await client.SendAsync(verifyRequest);

        Assert.Equal(HttpStatusCode.OK, verifyResponse.StatusCode);

        using var scope = fixture.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var domainUser = await users.GetByIdAsync(new UserId(user.Id));
        Assert.True(domainUser!.Mfa.IsEnrolled);
    }

    [Fact]
    public async Task Verifying_an_incorrect_code_is_rejected()
    {
        using var client = fixture.CreateClient();
        var user = await TestUsers.ProvisionAsync(client);
        var login = await TestUsers.LoginAsync(client, user.Username);
        await EnrollAsync(client, login.AccessToken);

        var verifyRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/identity/auth/mfa/verify")
        {
            Content = JsonContent.Create(new { code = "000000" }),
        }.WithBearerToken(login.AccessToken);
        var verifyResponse = await client.SendAsync(verifyRequest);

        Assert.Equal(HttpStatusCode.Unauthorized, verifyResponse.StatusCode);
    }

    [Fact]
    public async Task Login_for_a_user_holding_an_MFA_required_role_returns_a_challenge_not_tokens()
    {
        using var client = fixture.CreateClient();
        var user = await TestUsers.ProvisionAsync(client);
        await AssignMfaRequiredRoleAsync(user.Id);

        var response = await client.PostAsJsonAsync("/api/v1/identity/auth/login", new { identifier = user.Username, password = TestUsers.DefaultPassword });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var challenge = await response.Content.ReadFromJsonAsync<LoginRequiresMfaBody>();
        Assert.False(string.IsNullOrWhiteSpace(challenge!.MfaChallengeToken));
        Assert.False(challenge.MfaEnrolled);
    }

    [Fact]
    public async Task Completing_the_MFA_challenge_with_the_correct_code_issues_real_tokens_and_opens_a_Session()
    {
        using var client = fixture.CreateClient();
        var user = await TestUsers.ProvisionAsync(client);
        await AssignMfaRequiredRoleAsync(user.Id);

        // First login attempt only proves the password - the challenge token lets this User
        // enroll AND complete this same login in one verify call (MfaEnrollmentService's own
        // remarks on why one code proves both).
        var loginResponse = await client.PostAsJsonAsync("/api/v1/identity/auth/login", new { identifier = user.Username, password = TestUsers.DefaultPassword });
        var challenge = await loginResponse.Content.ReadFromJsonAsync<LoginRequiresMfaBody>();

        var enrollment = await EnrollAsync(client, challenge!.MfaChallengeToken);

        var verifyRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/identity/auth/mfa/verify")
        {
            Content = JsonContent.Create(new { code = ComputeValidCode(enrollment.SecretBase32) }),
        }.WithBearerToken(challenge.MfaChallengeToken);
        var verifyResponse = await client.SendAsync(verifyRequest);

        Assert.Equal(HttpStatusCode.OK, verifyResponse.StatusCode);
        var result = await verifyResponse.Content.ReadFromJsonAsync<MfaVerifyResultBody>();
        Assert.NotNull(result!.Tokens);
        Assert.False(string.IsNullOrWhiteSpace(result.Tokens!.AccessToken));
    }

    [Fact]
    public async Task An_MFA_challenge_token_cannot_be_used_against_a_live_session_only_endpoint()
    {
        using var client = fixture.CreateClient();
        var user = await TestUsers.ProvisionAsync(client);
        await AssignMfaRequiredRoleAsync(user.Id);
        var loginResponse = await client.PostAsJsonAsync("/api/v1/identity/auth/login", new { identifier = user.Username, password = TestUsers.DefaultPassword });
        var challenge = await loginResponse.Content.ReadFromJsonAsync<LoginRequiresMfaBody>();

        var logoutRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/identity/auth/logout").WithBearerToken(challenge!.MfaChallengeToken);
        var logoutResponse = await client.SendAsync(logoutRequest);

        Assert.Equal(HttpStatusCode.Forbidden, logoutResponse.StatusCode);
    }

    private static async Task<MfaEnrollmentDto> EnrollAsync(HttpClient client, string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/identity/auth/mfa/enroll").WithBearerToken(accessToken);
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<MfaEnrollmentDto>())!;
    }

    private async Task AssignMfaRequiredRoleAsync(Guid userId)
    {
        using var scope = fixture.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var roles = scope.ServiceProvider.GetRequiredService<IRoleRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var now = clock.UtcNow;
        var role = Role.Create($"IntegrationTestMfaRequiredRole-{Guid.NewGuid():N}", null, [], now, requiresMfa: true);
        roles.Add(role);

        var domainUser = await users.GetByIdAsync(new UserId(userId)) ?? throw new InvalidOperationException("Seeded User was not found immediately after provisioning.");
        domainUser.AssignRole(role.Id, scopeNode: null, now);

        await unitOfWork.SaveChangesAsync();
    }

    private static string ComputeValidCode(string secretBase32) => new Totp(Base32Encoding.ToBytes(secretBase32)).ComputeTotp();

    private sealed record LoginRequiresMfaBody(string MfaChallengeToken, DateTimeOffset MfaChallengeTokenExpiresAt, bool MfaEnrolled);

    private sealed record MfaVerifyResultBody(TokenPairResult? Tokens);
}
