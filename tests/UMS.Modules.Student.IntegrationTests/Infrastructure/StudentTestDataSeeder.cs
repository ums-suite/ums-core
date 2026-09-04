using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Identity.Application.Abstractions;
using UMS.Modules.Identity.Application.Auth;
using UMS.Modules.Identity.Application.Users;
using UMS.Modules.Identity.Domain.Roles;
using UMS.Modules.Identity.Domain.Users;
using UMS.Modules.Organization.Application.Campuses;
using UMS.Modules.Organization.Application.Departments;
using UMS.Modules.Organization.Application.Faculties;
using UMS.Modules.Organization.Application.Permissions;
using UMS.Modules.Organization.Application.Universities;
using UMS.Modules.Student.Application.Permissions;
using UMS.Shared.Student;

namespace UMS.Modules.Student.IntegrationTests.Infrastructure;

/// <summary>Test-only bootstrap for a User holding every Identity/Organization/Student Permission this suite needs, plus a real University -&gt; Campus -&gt; Faculty(org-unit) -&gt; Department chain to reference, and STU-1's own in-process <c>IStudentRecordProvisioner</c> seed-data path. Mirrors <c>UMS.Modules.Faculty.IntegrationTests.Infrastructure.FacultyTestDataSeeder</c> exactly.</summary>
public static class StudentTestDataSeeder
{
    public static async Task<(Guid UserId, string Username, string Password, string AccessToken)> ProvisionAdminAsync(StudentApiFixture fixture, HttpClient client)
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
                $"IntegrationTestStudentAdmin-{Guid.NewGuid():N}",
                "Full Identity/Organization/Student-permission Role used only by the integration test suite.",
                [
                    OrganizationPermissions.UniversityManage,
                    OrganizationPermissions.CampusManage,
                    OrganizationPermissions.FacultyManage,
                    OrganizationPermissions.DepartmentManage,
                    StudentPermissions.ProfileRead,
                    StudentPermissions.StatusChange,
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

    /// <summary>Seeds University -&gt; Campus -&gt; Faculty(org-unit) -&gt; Department through Organization's own real endpoints (never a direct DbContext write) - <c>CreateStudentRecordService</c>'s own Department existence check validates against this, so a fabricated id would fail realistically.</summary>
    public static async Task<Guid> SeedDepartmentAsync(HttpClient client, string adminAccessToken)
    {
        var university = await PostAsync<UniversityDto>(client, adminAccessToken, "/api/v1/organization/universities", new CreateUniversityRequest($"Test University {Guid.NewGuid():N}", null));
        var campus = await PostAsync<CampusDto>(client, adminAccessToken, "/api/v1/organization/campuses", new CreateCampusRequest(university.Id, $"Main Campus {Guid.NewGuid():N}"));
        var orgFaculty = await PostAsync<FacultyDto>(client, adminAccessToken, "/api/v1/organization/faculties", new CreateFacultyRequest(campus.Id, $"Faculty of Testing {Guid.NewGuid():N}", null));
        var department = await PostAsync<DepartmentDto>(client, adminAccessToken, "/api/v1/organization/departments", new CreateDepartmentRequest(orgFaculty.Id, $"Department of Testing {Guid.NewGuid():N}", null));
        return department.Id;
    }

    /// <summary>
    /// STU-1: exercises <c>IStudentRecordProvisioner</c> the same in-process way a future Admission
    /// module will (release/DEVELOPMENT_PLAN.md row 11's "seed-data testable" framing - the command
    /// has no public HTTP route at all, requirement-spec.md §6).
    /// </summary>
    public static async Task<StudentRecordSummary> CreateStudentRecordAsync(StudentApiFixture fixture, Guid departmentId, CreateStudentRecordCommand? overrides = null, Guid? programId = null)
    {
        using var scope = fixture.Services.CreateScope();
        var provisioner = scope.ServiceProvider.GetRequiredService<IStudentRecordProvisioner>();

        var command = overrides ?? new CreateStudentRecordCommand(
            Guid.NewGuid(),
            2026,
            "CSE",
            departmentId,
            programId ?? Guid.NewGuid(),
            "Rahim",
            "Uddin",
            null,
            null,
            $"student-{Guid.NewGuid():N}@example.edu.bd",
            null,
            new DateOnly(2005, 1, 1),
            "1234567890");

        var result = await provisioner.CreateAsync(command);
        if (result.IsFailure)
        {
            throw new InvalidOperationException($"CreateStudentRecord seed call failed: {result.Error!.Code} - {result.Error.Message}");
        }

        return result.Value;
    }

    /// <summary>
    /// STU-2's own generated initial password is a caller-invisible random secret by design (see
    /// <c>UMS.Shared.Identity.ProvisionUserCommand.Password</c>'s own remarks - a real Student is
    /// meant to reach it only through Identity's own forgot-password flow). This test-only helper
    /// resets it directly via the Identity domain aggregate's own <c>ChangePassword</c> method
    /// (never a raw SQL write) so self-service endpoint tests can log in as the Student's real
    /// provisioned Identity User with a known password, then logs in and returns the token pair.
    /// </summary>
    public static async Task<TokenPairResult> ResetPasswordAndLoginAsync(StudentApiFixture fixture, HttpClient client, Guid identityUserId, string newPassword)
    {
        string username;
        using (var scope = fixture.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var clock = scope.ServiceProvider.GetRequiredService<IClock>();

            var user = await users.GetByIdAsync(new UserId(identityUserId)) ?? throw new InvalidOperationException($"Identity User '{identityUserId}' was not found.");
            var now = clock.UtcNow;
            user.ChangePassword(Credential.FromHash(passwordHasher.HashPassword(newPassword), passwordHasher.AlgorithmName, now), now);
            username = user.Username;

            await unitOfWork.SaveChangesAsync();
        }

        return await TestUsers.LoginAsync(client, username, newPassword);
    }

    private static async Task<T> PostAsync<T>(HttpClient client, string accessToken, string path, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) }.WithBearerToken(accessToken);
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
}
