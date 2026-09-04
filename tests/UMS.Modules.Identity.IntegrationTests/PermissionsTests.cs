using System.Net;
using System.Net.Http.Json;
using UMS.Modules.Identity.Application.Permissions;
using UMS.Modules.Identity.IntegrationTests.Infrastructure;

namespace UMS.Modules.Identity.IntegrationTests;

/// <summary>IDN-1: the Permission catalog is synchronized from the manifest at Host startup and served via the API.</summary>
[Collection(IdentityApiTestCollectionDefinition.Name)]
public class PermissionsTests(IdentityApiFixture fixture)
{
    [Fact]
    public async Task GetPermissions_returns_every_manifest_registered_Identity_permission()
    {
        using var client = fixture.CreateClient();
        var (_, adminUsername, adminPassword, _) = await TestDataSeeder.ProvisionAdminAsync(fixture, client);
        var admin = await TestUsers.LoginAsync(client, adminUsername, adminPassword);

        var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/api/v1/identity/permissions").WithBearerToken(admin.AccessToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var catalog = await response.Content.ReadFromJsonAsync<List<PermissionCatalogEntryDto>>();
        var keys = catalog!.Select(p => p.Key).ToHashSet();
        Assert.Contains(IdentityPermissions.UserRead, keys);
        Assert.Contains(IdentityPermissions.RoleAssign, keys);
        Assert.Contains(IdentityPermissions.SessionRevoke, keys);

        // The catalog is shared platform-wide (identity §2/§9.2: "a module-registered manifest") -
        // once a second module (Audit, release/DEVELOPMENT_PLAN.md Flow #5) registers its own
        // IPermissionManifest, its entries legitimately appear here too. This assertion is scoped
        // to Identity's own contribution, not "every entry in the catalog".
        var identityOwned = catalog!.Where(p => p.Key.StartsWith("identity.", StringComparison.Ordinal));
        Assert.All(identityOwned, p => Assert.Equal("identity", p.OwningModule));
    }
}
