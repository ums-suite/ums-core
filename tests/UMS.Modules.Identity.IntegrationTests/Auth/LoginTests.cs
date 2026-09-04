using System.Net;
using System.Net.Http.Json;
using UMS.Modules.Identity.Application.Auth;
using UMS.Modules.Identity.IntegrationTests.Infrastructure;

namespace UMS.Modules.Identity.IntegrationTests.Auth;

[Collection(IdentityApiTestCollectionDefinition.Name)]
public class LoginTests(IdentityApiFixture fixture)
{
    [Fact]
    public async Task Login_with_the_correct_username_and_password_returns_a_token_pair()
    {
        using var client = fixture.CreateClient();
        var user = await TestUsers.ProvisionAsync(client);

        var response = await client.PostAsJsonAsync("/api/v1/identity/auth/login", new { identifier = user.Username, password = TestUsers.DefaultPassword });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var tokens = await response.Content.ReadFromJsonAsync<TokenPairResult>();
        Assert.False(string.IsNullOrWhiteSpace(tokens!.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(tokens.RefreshToken));
    }

    [Fact]
    public async Task Login_by_email_identifier_also_succeeds()
    {
        using var client = fixture.CreateClient();
        var user = await TestUsers.ProvisionAsync(client);

        var response = await client.PostAsJsonAsync("/api/v1/identity/auth/login", new { identifier = user.Email, password = TestUsers.DefaultPassword });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Login_with_the_wrong_password_returns_401_with_a_generic_error()
    {
        using var client = fixture.CreateClient();
        var user = await TestUsers.ProvisionAsync(client);

        var response = await client.PostAsJsonAsync("/api/v1/identity/auth/login", new { identifier = user.Username, password = "wrong-password" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_with_an_unknown_identifier_returns_the_same_401_as_a_wrong_password()
    {
        // requirement-spec.md identity §5 Security NFR - never reveal which half was wrong.
        using var client = fixture.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/identity/auth/login", new { identifier = $"nobody-{Guid.NewGuid():N}", password = "anything" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_permission_gated_endpoint_rejects_a_request_with_no_token_at_all()
    {
        using var client = fixture.CreateClient();

        var response = await client.GetAsync("/api/v1/identity/users");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Suspending_a_User_blocks_a_subsequent_login_even_with_the_correct_password()
    {
        using var client = fixture.CreateClient();
        var (_, adminUsername, adminPassword, _) = await TestDataSeeder.ProvisionAdminAsync(fixture, client);
        var adminTokens = await TestUsers.LoginAsync(client, adminUsername, adminPassword);
        var user = await TestUsers.ProvisionAsync(client);

        var suspendRequest = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/identity/users/{user.Id}/status")
        {
            Content = JsonContent.Create(new { status = "Suspended" }),
        }.WithBearerToken(adminTokens.AccessToken);
        var suspendResponse = await client.SendAsync(suspendRequest);
        Assert.Equal(HttpStatusCode.OK, suspendResponse.StatusCode);

        var loginResponse = await client.PostAsJsonAsync("/api/v1/identity/auth/login", new { identifier = user.Username, password = TestUsers.DefaultPassword });

        Assert.Equal(HttpStatusCode.Forbidden, loginResponse.StatusCode);
    }
}
