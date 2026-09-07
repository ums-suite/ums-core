using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Identity.Application.Abstractions;
using UMS.Modules.Identity.Application.Users;
using UMS.Modules.Identity.Domain.Roles;
using UMS.Modules.Identity.Domain.ScopeGrants;
using UMS.Modules.Identity.Domain.Users;
using UMS.Modules.Student.Application.Permissions;

namespace UMS.Modules.Student.IntegrationTests.Infrastructure;

/// <summary>STU-9..STU-16's own test-only reviewer/bulk-import-admin provisioning - mirrors <c>StudentTestDataSeeder.ProvisionAdminAsync</c> exactly, but grants a ScopeGrant-bounded Role instead of a university-wide one, so the ADR-0006 scope-check itself (requirement-spec.md §5) is exercised for real.</summary>
public static class StudentRequestTestDataSeeder
{
    /// <summary>Grants <see cref="StudentPermissions.RequestReview"/>, optionally scoped to a single Organization node (<see langword="null"/> for university-wide/Registrar-tier visibility).</summary>
    public static async Task<(Guid UserId, string Username, string Password, string AccessToken)> ProvisionReviewerAsync(StudentApiFixture fixture, HttpClient client, Guid? scopeNodeId)
    {
        return await ProvisionWithPermissionAsync(fixture, client, StudentPermissions.RequestReview, scopeNodeId);
    }

    public static async Task<(Guid UserId, string Username, string Password, string AccessToken)> ProvisionBulkImportAdminAsync(StudentApiFixture fixture, HttpClient client)
    {
        return await ProvisionWithPermissionAsync(fixture, client, StudentPermissions.BulkImportExecute, scopeNodeId: null);
    }

    /// <summary>
    /// STU-9/STU-10's own fulfillment step calls Documents for real (ADR-0010) - Documents' own
    /// generation pipeline requires a published <c>DocumentTemplate</c> for the requested
    /// <paramref name="documentType"/> to exist first (mirrors
    /// <c>UMS.Modules.Documents.IntegrationTests.BulkJobs.BulkGenerationEndpointsTests.PublishTemplateAsync</c>
    /// exactly) - without one, generation degrades to a documented, non-fatal failure and the
    /// StudentRequest stays <c>Approved</c> (fulfillment-pending) rather than reaching <c>Fulfilled</c>.
    /// Uses Documents' own raw permission string directly (<c>document.template.manage</c>) rather
    /// than taking a project reference on Documents' Application assembly just for one constant.
    /// </summary>
    public static async Task PublishDocumentTemplateAsync(StudentApiFixture fixture, HttpClient client, string documentType)
    {
        var (_, _, _, accessToken) = await ProvisionWithPermissionAsync(fixture, client, "document.template.manage", scopeNodeId: null);

        var body = new
        {
            documentType,
            layoutAssetKey = (string?)null,
            translations = new[] { new { language = "en", title = documentType, labelsJson = "{}" } },
        };

        var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/v1/documents/templates") { Content = JsonContent.Create(body) }.WithBearerToken(accessToken));
        response.EnsureSuccessStatusCode();
    }

    private static async Task<(Guid UserId, string Username, string Password, string AccessToken)> ProvisionWithPermissionAsync(StudentApiFixture fixture, HttpClient client, string permission, Guid? scopeNodeId)
    {
        var username = $"reviewer-{Guid.NewGuid():N}";
        var password = "a-Kn0wn-Passw0rd!";
        var email = $"{username}@example.edu.bd";

        var provisionResponse = await client.PostAsJsonAsync(
            "/api/v1/identity/users",
            new ProvisionUserRequest(username, email, "Reviewer", "User", null, null, null, null, password));
        provisionResponse.EnsureSuccessStatusCode();
        var user = await provisionResponse.Content.ReadFromJsonAsync<UserDto>();

        using (var scope = fixture.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            var roles = scope.ServiceProvider.GetRequiredService<IRoleRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var clock = scope.ServiceProvider.GetRequiredService<IClock>();

            var now = clock.UtcNow;
            var role = Role.Create($"IntegrationTestReviewer-{Guid.NewGuid():N}", "Test-only Role granting exactly one Student permission.", [permission], now);
            roles.Add(role);

            var domainUser = await users.GetByIdAsync(new UserId(user!.Id)) ?? throw new InvalidOperationException("Seeded reviewer User was not found immediately after provisioning.");
            domainUser.AssignRole(role.Id, scopeNode: scopeNodeId is { } nodeId ? new OrganizationNodeId(nodeId) : null, now);

            await unitOfWork.SaveChangesAsync();
        }

        var login = await TestUsers.LoginAsync(client, username, password);
        return (user!.Id, username, password, login.AccessToken);
    }
}
