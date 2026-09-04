using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Identity.Application.Abstractions;
using UMS.Modules.Identity.Application.Users;
using UMS.Modules.Identity.Domain.Roles;
using UMS.Modules.Identity.Domain.Users;
using UMS.Modules.Organization.Application.Permissions;

namespace UMS.Modules.Organization.IntegrationTests.Infrastructure;

/// <summary>
/// Test-only bootstrap for a User holding every Organization Permission - mirrors
/// <c>UMS.Modules.Identity.IntegrationTests.Infrastructure.TestDataSeeder</c> exactly (same "who
/// assigns the first Role-assigner" rationale). Provisions the User through the real
/// `POST /users` endpoint, then reaches into the DI container for Identity's own
/// <see cref="IRoleRepository"/>/<see cref="IUserRepository"/> ports - never a DbContext directly
/// - to grant the bundle.
/// </summary>
public static class TestDataSeeder
{
    public static async Task<(Guid UserId, string Username, string Password, string Email)> ProvisionAdminAsync(OrganizationApiFixture fixture, HttpClient client)
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
            $"IntegrationTestOrgAdmin-{Guid.NewGuid():N}",
            "Full Organization-permission Role used only by the integration test suite.",
            [
                OrganizationPermissions.UniversityManage,
                OrganizationPermissions.CampusManage,
                OrganizationPermissions.FacultyManage,
                OrganizationPermissions.DepartmentManage,
                OrganizationPermissions.ProgramManage,
                OrganizationPermissions.DesignationManage,
                OrganizationPermissions.FacilityManage,
            ],
            now);
        roles.Add(role);

        var domainUser = await users.GetByIdAsync(new UserId(user!.Id)) ?? throw new InvalidOperationException("Seeded admin User was not found immediately after provisioning.");
        domainUser.AssignRole(role.Id, scopeNode: null, now);

        await unitOfWork.SaveChangesAsync();

        return (user.Id, username, password, email);
    }
}
