using System.Net;
using System.Net.Http.Json;
using UMS.Modules.Identity.Application.Users;
using UMS.Modules.Identity.IntegrationTests.Infrastructure;

namespace UMS.Modules.Identity.IntegrationTests.Users;

/// <summary>IDN-2/IDN-3, exercised over real HTTP + Postgres, including the "duplicate User" concurrency edge case.</summary>
[Collection(IdentityApiTestCollectionDefinition.Name)]
public class ProvisioningTests(IdentityApiFixture fixture)
{
    [Fact]
    public async Task Provisioning_a_User_then_fetching_it_by_id_returns_the_same_record()
    {
        using var client = fixture.CreateClient();
        var (_, adminUsername, adminPassword, _) = await TestDataSeeder.ProvisionAdminAsync(fixture, client);
        var admin = await TestUsers.LoginAsync(client, adminUsername, adminPassword);
        var user = await TestUsers.ProvisionAsync(client);

        var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, $"/api/v1/identity/users/{user.Id}").WithBearerToken(admin.AccessToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var fetched = await response.Content.ReadFromJsonAsync<UserDto>();
        Assert.Equal(user.Id, fetched!.Id);
    }

    [Fact]
    public async Task Provisioning_with_a_duplicate_email_from_a_different_username_links_to_the_existing_User_rather_than_conflicting()
    {
        // edge-cases.md's "same person, one identity" resolution is not limited to a literal
        // concurrent race - IDN-2's provisioning service treats email as the stable identifier
        // and links to the existing User for ANY email collision (see
        // UserProvisioningService.ProvisionAsync's DuplicateUserException("email", ...) handling).
        // A genuinely different person colliding on USERNAME (not email) is the real conflict case
        // - covered by the test below.
        using var client = fixture.CreateClient();
        var sharedEmail = $"shared-{Guid.NewGuid():N}@example.edu.bd";

        var first = await client.PostAsJsonAsync(
            "/api/v1/identity/users",
            new ProvisionUserRequest("first-user", sharedEmail, "First", "User", null, null, null, null, TestUsers.DefaultPassword));
        first.EnsureSuccessStatusCode();
        var firstUser = await first.Content.ReadFromJsonAsync<UserDto>();

        var second = await client.PostAsJsonAsync(
            "/api/v1/identity/users",
            new ProvisionUserRequest("second-user", sharedEmail, "Second", "User", null, null, null, null, TestUsers.DefaultPassword));

        Assert.True(second.IsSuccessStatusCode, $"Expected success, got {second.StatusCode}");
        var secondUser = await second.Content.ReadFromJsonAsync<UserDto>();
        Assert.Equal(firstUser!.Id, secondUser!.Id);
    }

    [Fact]
    public async Task Provisioning_with_a_duplicate_username_but_a_different_email_is_a_genuine_conflict()
    {
        using var client = fixture.CreateClient();
        var sharedUsername = $"shared-{Guid.NewGuid():N}";

        var first = await client.PostAsJsonAsync(
            "/api/v1/identity/users",
            new ProvisionUserRequest(sharedUsername, $"first-{Guid.NewGuid():N}@example.edu.bd", "First", "User", null, null, null, null, TestUsers.DefaultPassword));
        first.EnsureSuccessStatusCode();

        var second = await client.PostAsJsonAsync(
            "/api/v1/identity/users",
            new ProvisionUserRequest(sharedUsername, $"second-{Guid.NewGuid():N}@example.edu.bd", "Second", "User", null, null, null, null, TestUsers.DefaultPassword));

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Concurrent_provisioning_flows_racing_on_the_same_email_resolve_to_exactly_one_User()
    {
        // edge-cases.md, "Concurrent provisioning creates a duplicate User for the same person":
        // two different onboarding flows (different usernames, same stable email identifier)
        // racing must never produce two User rows.
        using var client1 = fixture.CreateClient();
        using var client2 = fixture.CreateClient();
        var sharedEmail = $"race-{Guid.NewGuid():N}@example.edu.bd";

        var requestA = new ProvisionUserRequest($"race-a-{Guid.NewGuid():N}", sharedEmail, "Race", "A", null, null, null, null, TestUsers.DefaultPassword);
        var requestB = new ProvisionUserRequest($"race-b-{Guid.NewGuid():N}", sharedEmail, "Race", "B", null, null, null, null, TestUsers.DefaultPassword);

        var responses = await Task.WhenAll(
            client1.PostAsJsonAsync("/api/v1/identity/users", requestA),
            client2.PostAsJsonAsync("/api/v1/identity/users", requestB));

        Assert.All(responses, r => Assert.True(r.IsSuccessStatusCode, $"Expected success, got {r.StatusCode}"));

        var users = await Task.WhenAll(responses.Select(r => r.Content.ReadFromJsonAsync<UserDto>()));
        Assert.Equal(users[0]!.Id, users[1]!.Id);
    }

    [Fact]
    public async Task Listing_users_requires_the_identity_user_read_permission()
    {
        using var client = fixture.CreateClient();
        var user = await TestUsers.ProvisionAsync(client);
        var login = await TestUsers.LoginAsync(client, user.Username);

        var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/api/v1/identity/users").WithBearerToken(login.AccessToken));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
