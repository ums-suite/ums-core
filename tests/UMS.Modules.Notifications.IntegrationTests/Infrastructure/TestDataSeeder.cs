using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Identity.Application.Abstractions;
using UMS.Modules.Identity.Application.Users;
using UMS.Modules.Identity.Domain.Roles;
using UMS.Modules.Identity.Domain.Users;
using UMS.Modules.Notifications.Application.Permissions;

namespace UMS.Modules.Notifications.IntegrationTests.Infrastructure;

/// <summary>Test-only bootstrap for a User holding Notifications' own admin/ops Permissions - mirrors Audit's own <c>TestDataSeeder</c>.</summary>
public static class TestDataSeeder
{
    public static async Task<(Guid UserId, string Username, string Password)> ProvisionNotificationsAdminAsync(NotificationsApiFixture fixture, HttpClient client)
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
            $"IntegrationTestNotificationsAdmin-{Guid.NewGuid():N}",
            "Full Notifications-permission Role used only by the integration test suite.",
            [
                NotificationsPermissions.TemplateManage,
                NotificationsPermissions.RequestRead,
                NotificationsPermissions.DeadLetterRead,
            ],
            now);
        roles.Add(role);

        var domainUser = await users.GetByIdAsync(new UserId(user!.Id)) ?? throw new InvalidOperationException("Seeded admin User was not found immediately after provisioning.");
        domainUser.AssignRole(role.Id, scopeNode: null, now);

        await unitOfWork.SaveChangesAsync();

        return (user.Id, username, password);
    }
}
