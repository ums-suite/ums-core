using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Identity.Application.Abstractions;
using UMS.Modules.Identity.Application.Permissions;
using UMS.Modules.Identity.Application.Users;
using UMS.Modules.Identity.Domain.Roles;
using UMS.Modules.Identity.Domain.Users;

namespace UMS.Modules.Identity.IntegrationTests.Infrastructure;

/// <summary>
/// Test-only bootstrap for a User already holding every Identity Permission - real systems solve
/// this "who assigns the first Role-assigner" problem with a deployment-time seed migration, which
/// is out of this flow's ticket list. Provisions the User through the real
/// <c>POST /users</c> endpoint (so Credential/hashing goes through the real path), then reaches
/// into the DI container for the same <see cref="IRoleRepository"/>/<see cref="IUserRepository"/>
/// ports an application service would use - never the DbContext directly - to grant the bundle.
/// </summary>
public static class TestDataSeeder
{
    public static async Task<(Guid UserId, string Username, string Password, string Email)> ProvisionAdminAsync(IdentityApiFixture fixture, HttpClient client)
    {
        var username = $"admin-{Guid.NewGuid():N}";
        var password = "Sup3rSecret!Password1";
        var email = $"{username}@example.edu.bd";

        var provisionResponse = await client.PostAsJsonAsync(
            "/api/v1/identity/users",
            new ProvisionUserRequest(username, email, "Admin", "User", null, null, null, null, password));
        provisionResponse.EnsureSuccessStatusCode();
        var user = await provisionResponse.Content.ReadFromJsonAsync<UserDto>();

        using var scope = fixture.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var roles = scope.ServiceProvider.GetRequiredService<IRoleRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var now = clock.UtcNow;
        var role = Role.Create(
            $"IntegrationTestAdmin-{Guid.NewGuid():N}",
            "Full-permission Role used only by the integration test suite to bootstrap admin actions.",
            [
                IdentityPermissions.UserRead,
                IdentityPermissions.UserManage,
                IdentityPermissions.RoleManage,
                IdentityPermissions.RoleAssign,
                IdentityPermissions.PermissionRead,
            ],
            now);
        roles.Add(role);

        var domainUser = await users.GetByIdAsync(new UserId(user!.Id)) ?? throw new InvalidOperationException("Seeded admin User was not found immediately after provisioning.");
        domainUser.AssignRole(role.Id, scopeNode: null, now);

        await unitOfWork.SaveChangesAsync();

        return (user.Id, username, password, email);
    }
}
