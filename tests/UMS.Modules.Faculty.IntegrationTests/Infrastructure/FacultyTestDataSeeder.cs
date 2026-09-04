using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Faculty.Application.Permissions;
using UMS.Modules.Identity.Application.Abstractions;
using UMS.Modules.Identity.Application.Users;
using UMS.Modules.Identity.Domain.Roles;
using UMS.Modules.Identity.Domain.Users;
using UMS.Modules.Organization.Application.Campuses;
using UMS.Modules.Organization.Application.Departments;
using UMS.Modules.Organization.Application.Designations;
using UMS.Modules.Organization.Application.Faculties;
using UMS.Modules.Organization.Application.Permissions;
using UMS.Modules.Organization.Application.Universities;

namespace UMS.Modules.Faculty.IntegrationTests.Infrastructure;

/// <summary>Test-only bootstrap for a User holding every Identity/Organization/Faculty Permission this suite needs, plus a real University -&gt; Campus -&gt; Faculty(org-unit) -&gt; Department chain to reference. Mirrors <c>UMS.Modules.Organization.IntegrationTests.Infrastructure.TestDataSeeder</c> exactly.</summary>
public static class FacultyTestDataSeeder
{
    public static async Task<(Guid UserId, string Username, string Password, string AccessToken)> ProvisionAdminAsync(FacultyApiFixture fixture, HttpClient client)
    {
        var username = $"admin-{Guid.NewGuid():N}";
        var password = "Sup3rSecret!Password1";
        var email = $"{username}@example.edu.bd";

        var provisionResponse = await client.PostAsJsonAsync(
            "/api/v1/identity/users",
            new ProvisionUserRequest(username, email, "Admin", "User", null, null, null, null, password));
        provisionResponse.EnsureSuccessStatusCode();
        var user = await provisionResponse.Content.ReadFromJsonAsync<UserDto>();

        using (var scope = fixture.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            var roles = scope.ServiceProvider.GetRequiredService<IRoleRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var clock = scope.ServiceProvider.GetRequiredService<IClock>();

            var now = clock.UtcNow;
            var role = Role.Create(
                $"IntegrationTestFacultyAdmin-{Guid.NewGuid():N}",
                "Full Identity/Organization/Faculty-permission Role used only by the integration test suite.",
                [
                    OrganizationPermissions.UniversityManage,
                    OrganizationPermissions.CampusManage,
                    OrganizationPermissions.FacultyManage,
                    OrganizationPermissions.DepartmentManage,
                    OrganizationPermissions.DesignationManage,
                    FacultyPermissions.ProfileRead,
                    FacultyPermissions.MemberManage,
                    FacultyPermissions.CourseAssignmentRead,
                    FacultyPermissions.LeaveApproveDepartment,
                    FacultyPermissions.LeaveApproveAuthority,
                    FacultyPermissions.ResearchUpdate,
                ],
                now);
            roles.Add(role);

            var domainUser = await users.GetByIdAsync(new UserId(user!.Id)) ?? throw new InvalidOperationException("Seeded admin User was not found immediately after provisioning.");
            domainUser.AssignRole(role.Id, scopeNode: null, now);

            await unitOfWork.SaveChangesAsync();
        }

        var login = await TestUsers.LoginAsync(client, username, password);
        return (user!.Id, username, password, login.AccessToken);
    }

    /// <summary>Grants an ordinary (non-admin) User the baseline "Faculty" Permission bundle a real FacultyMember role would carry - <c>faculty.leave.create</c>/<c>faculty.profile.update</c>/<c>faculty.research.publish</c> - so a plain provisioned test User can actually submit their own LeaveRequest, self-service-update their own profile, or publish their own ResearchProfile (§16 role matrix's own "Faculty: Full only on their own profile subset" framing, not the HR/Registrar bundle <see cref="ProvisionAdminAsync"/> grants).</summary>
    public static async Task GrantFacultyMemberRoleAsync(FacultyApiFixture fixture, Guid userId)
    {
        using var scope = fixture.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var roles = scope.ServiceProvider.GetRequiredService<IRoleRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var now = clock.UtcNow;
        var role = Role.Create(
            $"IntegrationTestFacultyMember-{Guid.NewGuid():N}",
            "Baseline FacultyMember-self-service Role used only by the integration test suite.",
            [FacultyPermissions.LeaveCreate, FacultyPermissions.ProfileUpdate, FacultyPermissions.ResearchPublish],
            now);
        roles.Add(role);

        var domainUser = await users.GetByIdAsync(new UserId(userId)) ?? throw new InvalidOperationException("Target User was not found.");
        domainUser.AssignRole(role.Id, scopeNode: null, now);

        await unitOfWork.SaveChangesAsync();
    }

    /// <summary>Seeds University -&gt; Campus -&gt; Faculty(org-unit) -&gt; Department through Organization's own real endpoints (never a direct DbContext write) - Faculty's own onboarding path validates the Department exists via <c>IOrganizationDepartmentExistenceChecker</c>, so a fabricated id would fail realistically.</summary>
    public static async Task<Guid> SeedDepartmentAsync(HttpClient client, string adminAccessToken)
    {
        var university = await PostAsync<UniversityDto>(client, adminAccessToken, "/api/v1/organization/universities", new CreateUniversityRequest($"Test University {Guid.NewGuid():N}", null));
        var campus = await PostAsync<CampusDto>(client, adminAccessToken, "/api/v1/organization/campuses", new CreateCampusRequest(university.Id, $"Main Campus {Guid.NewGuid():N}"));
        var orgFaculty = await PostAsync<FacultyDto>(client, adminAccessToken, "/api/v1/organization/faculties", new CreateFacultyRequest(campus.Id, $"Faculty of Testing {Guid.NewGuid():N}", null));
        var department = await PostAsync<DepartmentDto>(client, adminAccessToken, "/api/v1/organization/departments", new CreateDepartmentRequest(orgFaculty.Id, $"Department of Testing {Guid.NewGuid():N}", null));
        return department.Id;
    }

    public static async Task<Guid> SeedDesignationAsync(HttpClient client, string adminAccessToken)
    {
        var designation = await PostAsync<DesignationDto>(client, adminAccessToken, "/api/v1/organization/designations", new CreateDesignationRequest($"Lecturer {Guid.NewGuid():N}", null));
        return designation.Id;
    }

    private static async Task<T> PostAsync<T>(HttpClient client, string accessToken, string path, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) }.WithBearerToken(accessToken);
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
}
