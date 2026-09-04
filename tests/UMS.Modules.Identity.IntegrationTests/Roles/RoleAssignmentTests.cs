using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Identity.Application.Permissions;
using UMS.Modules.Identity.Application.Roles;
using UMS.Modules.Identity.IntegrationTests.Infrastructure;
using UMS.Modules.Organization.Application.Abstractions;
using UMS.Modules.Organization.Domain.Universities;

namespace UMS.Modules.Identity.IntegrationTests.Roles;

/// <summary>IDN-14/IDN-15, over real HTTP - Role creation, assignment, and the "Role revoked mid-session" edge case.</summary>
[Collection(IdentityApiTestCollectionDefinition.Name)]
public class RoleAssignmentTests(IdentityApiFixture fixture)
{
    [Fact]
    public async Task Creating_a_role_with_an_unknown_permission_key_is_rejected()
    {
        using var client = fixture.CreateClient();
        var (_, adminUsername, adminPassword, _) = await TestDataSeeder.ProvisionAdminAsync(fixture, client);
        var admin = await TestUsers.LoginAsync(client, adminUsername, adminPassword);

        var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/v1/identity/roles")
        {
            Content = JsonContent.Create(new CreateRoleRequest("Bogus Role", null, ["not.a.real.permission"])),
        }.WithBearerToken(admin.AccessToken));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Assigning_a_role_with_a_ScopeGrant_organization_node_validates_against_the_real_Organization_module()
    {
        // requirement-spec.md identity §7: Identity resolves ScopeGrant existence via
        // Organization's own read interface. Organization now exists (release/DEVELOPMENT_PLAN.md
        // Flow #6) - the former StubOrganizationNodeExistenceChecker's "everything exists"
        // behavior is replaced by a real check against Organization's own tables (see
        // UMS.Modules.Identity.Infrastructure.Organization.OrganizationNodeExistenceCheckerAdapter).
        // A real OrganizationNodeId is accepted; a fabricated one is now correctly rejected -
        // this closes the gap this test itself used to document as "today's stub checker."
        using var client = fixture.CreateClient();
        var (_, adminUsername, adminPassword, _) = await TestDataSeeder.ProvisionAdminAsync(fixture, client);
        var admin = await TestUsers.LoginAsync(client, adminUsername, adminPassword);
        var role = await CreateRoleAsync(client, admin.AccessToken, [IdentityPermissions.UserRead]);

        // Seeds a real University directly through Organization's own repository port (never a
        // DbContext directly) - mirrors TestDataSeeder.cs's own reach-in for Identity's own
        // repositories.
        using var scope = fixture.Services.CreateScope();
        var universities = scope.ServiceProvider.GetRequiredService<IUniversityRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var university = University.Create($"Test University {Guid.NewGuid():N}", null, DateTimeOffset.UtcNow);
        universities.Add(university);
        await unitOfWork.SaveChangesAsync();

        var realNodeUser = await TestUsers.ProvisionAsync(client);
        var realNodeResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, $"/api/v1/identity/users/{realNodeUser.Id}/roles")
        {
            Content = JsonContent.Create(new AssignRoleRequest(role.Id, university.Id.Value)),
        }.WithBearerToken(admin.AccessToken));
        Assert.Equal(HttpStatusCode.OK, realNodeResponse.StatusCode);

        var fakeNodeUser = await TestUsers.ProvisionAsync(client);
        var fakeNodeResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, $"/api/v1/identity/users/{fakeNodeUser.Id}/roles")
        {
            Content = JsonContent.Create(new AssignRoleRequest(role.Id, Guid.NewGuid())),
        }.WithBearerToken(admin.AccessToken));
        Assert.Equal(HttpStatusCode.BadRequest, fakeNodeResponse.StatusCode);
    }

    [Fact]
    public async Task Assigning_a_role_grants_its_permissions_on_the_very_next_request()
    {
        using var client = fixture.CreateClient();
        var (_, adminUsername, adminPassword, _) = await TestDataSeeder.ProvisionAdminAsync(fixture, client);
        var admin = await TestUsers.LoginAsync(client, adminUsername, adminPassword);
        var user = await TestUsers.ProvisionAsync(client);
        var login = await TestUsers.LoginAsync(client, user.Username);
        var role = await CreateRoleAsync(client, admin.AccessToken, [IdentityPermissions.UserRead]);

        // Before assignment: denied.
        var beforeResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/api/v1/identity/users").WithBearerToken(login.AccessToken));
        Assert.Equal(HttpStatusCode.Forbidden, beforeResponse.StatusCode);

        var assignResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, $"/api/v1/identity/users/{user.Id}/roles")
        {
            Content = JsonContent.Create(new AssignRoleRequest(role.Id, null)),
        }.WithBearerToken(admin.AccessToken));
        assignResponse.EnsureSuccessStatusCode();
        var assignment = await assignResponse.Content.ReadFromJsonAsync<UserRoleAssignmentDto>();

        // Same still-unexpired access token, no re-login - the live permission cache picks up the
        // grant on the very next call (design-decisions.md, "Permission-Check Caching vs. Live Lookup").
        var afterResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/api/v1/identity/users").WithBearerToken(login.AccessToken));
        Assert.Equal(HttpStatusCode.OK, afterResponse.StatusCode);

        // "Role revoked mid-session" (edge-cases.md): revoking takes effect immediately too.
        var revokeResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/identity/users/{user.Id}/roles/{assignment!.AssignmentId}").WithBearerToken(admin.AccessToken));
        Assert.Equal(HttpStatusCode.NoContent, revokeResponse.StatusCode);

        var afterRevokeResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/api/v1/identity/users").WithBearerToken(login.AccessToken));
        Assert.Equal(HttpStatusCode.Forbidden, afterRevokeResponse.StatusCode);
    }

    [Fact]
    public async Task Assigning_the_same_role_and_scope_twice_returns_a_conflict()
    {
        using var client = fixture.CreateClient();
        var (_, adminUsername, adminPassword, _) = await TestDataSeeder.ProvisionAdminAsync(fixture, client);
        var admin = await TestUsers.LoginAsync(client, adminUsername, adminPassword);
        var user = await TestUsers.ProvisionAsync(client);
        var role = await CreateRoleAsync(client, admin.AccessToken, [IdentityPermissions.UserRead]);

        var first = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, $"/api/v1/identity/users/{user.Id}/roles") { Content = JsonContent.Create(new AssignRoleRequest(role.Id, null)) }.WithBearerToken(admin.AccessToken));
        first.EnsureSuccessStatusCode();

        var second = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, $"/api/v1/identity/users/{user.Id}/roles") { Content = JsonContent.Create(new AssignRoleRequest(role.Id, null)) }.WithBearerToken(admin.AccessToken));

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    private static async Task<RoleDto> CreateRoleAsync(HttpClient client, string adminAccessToken, IReadOnlyCollection<string> permissions)
    {
        var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/v1/identity/roles")
        {
            Content = JsonContent.Create(new CreateRoleRequest($"Role-{Guid.NewGuid():N}", null, permissions)),
        }.WithBearerToken(adminAccessToken));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<RoleDto>())!;
    }
}
