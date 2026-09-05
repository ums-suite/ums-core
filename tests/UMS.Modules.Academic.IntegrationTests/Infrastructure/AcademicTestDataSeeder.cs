using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Academic.Application.AcademicSessions;
using UMS.Modules.Academic.Application.Courses;
using UMS.Modules.Academic.Application.CourseOfferings;
using UMS.Modules.Academic.Application.Permissions;
using UMS.Modules.Academic.Application.Programs;
using UMS.Modules.Faculty.Application.FacultyMembers;
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
using UMS.Modules.Student.Application.Common;
using UMS.Modules.Student.Application.Permissions;
using UMS.Modules.Student.Application.Students;
using UMS.Shared.Student;

namespace UMS.Modules.Academic.IntegrationTests.Infrastructure;

/// <summary>Test-only bootstrap for everything Academic's own integration suite needs: an admin User with every Identity/Organization/Faculty/Student/Academic Permission this suite exercises, a real Organization chain, a real onboarded FacultyMember (login-capable), and a real Active Student (login-capable) via <c>IStudentRecordProvisioner</c> - mirrors <c>StudentTestDataSeeder</c>/<c>FacultyTestDataSeeder</c> exactly, extended for Academic's own wider cross-module surface.</summary>
public static class AcademicTestDataSeeder
{
    public static async Task<(Guid UserId, string Username, string Password, string AccessToken)> ProvisionAdminAsync(AcademicApiFixture fixture, HttpClient client)
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
                $"IntegrationTestAcademicAdmin-{Guid.NewGuid():N}",
                "Full Identity/Organization/Faculty/Student/Academic-permission Role used only by the integration test suite.",
                [
                    OrganizationPermissions.UniversityManage,
                    OrganizationPermissions.CampusManage,
                    OrganizationPermissions.FacultyManage,
                    OrganizationPermissions.DepartmentManage,
                    OrganizationPermissions.DesignationManage,
                    FacultyPermissions.MemberManage,
                    StudentPermissions.ProfileRead,
                    StudentPermissions.StatusChange,
                    AcademicPermissions.ProgramManage,
                    AcademicPermissions.CurriculumManage,
                    AcademicPermissions.CourseManage,
                    AcademicPermissions.AcademicSessionManage,
                    AcademicPermissions.CourseOfferingManage,
                    AcademicPermissions.EnrollmentApprove,
                    AcademicPermissions.GradeLock,
                    AcademicPermissions.GradeCorrect,
                    AcademicPermissions.ResultApprove,
                    AcademicPermissions.ResultPublish,
                    AcademicPermissions.StudentResultRead,
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

    public static async Task<Guid> SeedDepartmentAsync(HttpClient client, string adminAccessToken)
    {
        var university = await PostAsync<UniversityDto>(client, adminAccessToken, "/api/v1/organization/universities", new CreateUniversityRequest($"Test University {Guid.NewGuid():N}", null));
        var campus = await PostAsync<CampusDto>(client, adminAccessToken, "/api/v1/organization/campuses", new CreateCampusRequest(university.Id, $"Main Campus {Guid.NewGuid():N}"));
        var orgFaculty = await PostAsync<FacultyDto>(client, adminAccessToken, "/api/v1/organization/faculties", new CreateFacultyRequest(campus.Id, $"Faculty of Testing {Guid.NewGuid():N}", null));
        var department = await PostAsync<DepartmentDto>(client, adminAccessToken, "/api/v1/organization/departments", new CreateDepartmentRequest(orgFaculty.Id, $"Department of Testing {Guid.NewGuid():N}", null));
        return department.Id;
    }

    public static async Task<ProgramDto> SeedProgramAsync(HttpClient client, string adminAccessToken, Guid departmentId, int maxCreditsPerSemester = 15, bool requiresAdvisorApproval = false) =>
        await PostAsync<ProgramDto>(client, adminAccessToken, "/api/v1/academic/programs", new CreateProgramRequest(departmentId, $"PROG-{Guid.NewGuid():N}"[..12], "Test Program", maxCreditsPerSemester, requiresAdvisorApproval));

    public static async Task<CourseDto> SeedCourseAsync(HttpClient client, string adminAccessToken, int creditHours = 3, IReadOnlyCollection<Guid>? prerequisiteCourseIds = null) =>
        await PostAsync<CourseDto>(client, adminAccessToken, "/api/v1/academic/courses", new CreateCourseRequest($"CRS-{Guid.NewGuid():N}"[..10], "Test Course", creditHours, prerequisiteCourseIds));

    /// <summary>A Semester whose registration AND drop windows are both already open (spanning yesterday through 60 days from now) so ACD-6/ACD-7's window checks pass without any test needing to fake the clock. Each call uses a distinct, effectively-collision-free <c>AcademicSessionCode</c> (a random far-future year pair) since many test methods run against the SAME shared Postgres container within one test collection and <c>AcademicSession.Code</c> is unique.</summary>
    public static async Task<(Guid AcademicSessionId, Guid SemesterId)> SeedOpenSemesterAsync(HttpClient client, string adminAccessToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var yesterday = today.AddDays(-1);
        var future = today.AddDays(60);
        var year = System.Threading.Interlocked.Increment(ref _nextAcademicSessionYear);

        var session = await PostAsync<AcademicSessionDto>(
            client,
            adminAccessToken,
            "/api/v1/academic/academic-sessions",
            new CreateAcademicSessionRequest($"{year}-{year + 1}", [new CreateSemesterRequest("Test Semester", yesterday, future, yesterday, future)]));

        return (session.Id, session.Semesters.Single().Id);
    }

    // AcademicSessionCode is strictly "YYYY-YYYY" (4-digit years) - stays comfortably within that
    // bound while still starting from a randomized base so repeated test runs against a fresh
    // container don't depend on any particular starting value.
    private static int _nextAcademicSessionYear = 2100 + Random.Shared.Next(0, 3000);

    public static async Task<CourseOfferingDto> SeedCourseOfferingAsync(HttpClient client, string adminAccessToken, Guid courseId, Guid semesterId, Guid departmentId, int capacity, int sectionCount = 1)
    {
        var sections = Enumerable.Range(0, sectionCount)
            .Select(i => new CreateSectionRequest($"SEC-{(char)('A' + i)}", DayOfWeek.Monday, new TimeOnly(9 + i, 0), new TimeOnly(10 + i, 0)))
            .ToList();

        return await PostAsync<CourseOfferingDto>(client, adminAccessToken, "/api/v1/academic/course-offerings", new CreateCourseOfferingRequest(courseId, semesterId, departmentId, capacity, sections));
    }

    public static async Task<CourseOfferingDto> AddExamAsync(HttpClient client, string adminAccessToken, Guid courseOfferingId, IReadOnlyCollection<CreateAssessmentRequest> assessments) =>
        await PostAsync<CourseOfferingDto>(client, adminAccessToken, $"/api/v1/academic/course-offerings/{courseOfferingId}/exams", new CreateExamRequest("Combined", assessments));

    public static async Task<Guid> SeedDesignationAsync(HttpClient client, string adminAccessToken)
    {
        var designation = await PostAsync<DesignationDto>(client, adminAccessToken, "/api/v1/organization/designations", new CreateDesignationRequest($"Lecturer {Guid.NewGuid():N}", null));
        return designation.Id;
    }

    /// <summary>Onboards a real FacultyMember (via Faculty's own real endpoint - the FAC-4 outbox-relay/Faculty-lookup contracts Academic depends on are exercised for real), grants them the baseline Academic instructor Permission bundle (<c>grade.enter</c>/<c>attendance.record</c> - a real Instructor's own scope, not the HR/Registrar admin bundle), and logs in as their backing User.</summary>
    public static async Task<(Guid FacultyMemberId, Guid UserId, string AccessToken)> SeedFacultyMemberAsync(AcademicApiFixture fixture, HttpClient client, string adminAccessToken, Guid departmentId, Guid designationId)
    {
        var user = await TestUsers.ProvisionAsync(client);
        var facultyMember = await PostAsync<FacultyMemberDto>(
            client,
            adminAccessToken,
            "/api/v1/faculty/members",
            new OnboardFacultyMemberRequest(user.Id, $"EMP-{Guid.NewGuid():N}"[..10], departmentId, designationId, "FullTime", DateOnly.FromDateTime(DateTime.UtcNow)));

        using (var scope = fixture.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            var roles = scope.ServiceProvider.GetRequiredService<IRoleRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var clock = scope.ServiceProvider.GetRequiredService<IClock>();

            var now = clock.UtcNow;
            var role = Role.Create(
                $"IntegrationTestInstructor-{Guid.NewGuid():N}",
                "Baseline Instructor Permission bundle used only by the integration test suite.",
                [AcademicPermissions.GradeEnter, AcademicPermissions.AttendanceRecord],
                now);
            roles.Add(role);

            var domainUser = await users.GetByIdAsync(new UserId(user.Id)) ?? throw new InvalidOperationException("Seeded FacultyMember's backing User was not found immediately after provisioning.");
            domainUser.AssignRole(role.Id, scopeNode: null, now);

            await unitOfWork.SaveChangesAsync();
        }

        var login = await TestUsers.LoginAsync(client, user.Username);
        return (facultyMember.Id, user.Id, login.AccessToken);
    }

    /// <summary>
    /// STU-1: creates a real Student in-process via <c>IStudentRecordProvisioner</c> (mirrors
    /// <c>StudentTestDataSeeder.CreateStudentRecordAsync</c> exactly - the command has no public
    /// HTTP route), then moves it Enrolled -&gt; Active in-process via Student's own
    /// <c>StudentStatusService</c> (ACD-6's Enrollment gate requires `Active`) - bypassing HTTP for
    /// this internal setup step is the same "seed-data testable" posture STU-1's own contract
    /// doc comment describes, not a shortcut around any real invariant. Returns a login-capable
    /// access token for the Student's own backing Identity User.
    /// </summary>
    public static async Task<(Guid StudentId, string AccessToken)> SeedActiveStudentAsync(AcademicApiFixture fixture, HttpClient client, Guid departmentId, Guid programId)
    {
        using var scope = fixture.Services.CreateScope();
        var provisioner = scope.ServiceProvider.GetRequiredService<IStudentRecordProvisioner>();
        var statusService = scope.ServiceProvider.GetRequiredService<UMS.Modules.Student.Application.Students.StudentStatusService>();
        var students = scope.ServiceProvider.GetRequiredService<UMS.Modules.Student.Application.Abstractions.IStudentRepository>();

        var command = new CreateStudentRecordCommand(
            Guid.NewGuid(),
            2027,
            "TST",
            departmentId,
            programId,
            "Karim",
            "Rahman",
            null,
            null,
            $"student-{Guid.NewGuid():N}@example.edu.bd",
            null,
            new DateOnly(2005, 6, 15),
            null);

        var created = await provisioner.CreateAsync(command);
        if (created.IsFailure)
        {
            throw new InvalidOperationException($"CreateStudentRecord seed call failed: {created.Error!.Code} - {created.Error.Message}");
        }

        var freshlyCreated = await students.GetByIdAsync(new UMS.Modules.Student.Domain.Students.StudentId(created.Value.StudentId))
            ?? throw new InvalidOperationException("Seeded Student was not found immediately after creation.");
        var audit = new AuditContext(Guid.NewGuid(), null, $"test-seed-{Guid.NewGuid():N}");
        var activate = await statusService.ChangeStatusAsync(created.Value.StudentId, Guid.NewGuid(), new ChangeStudentStatusRequest("Active", "Seeded Active for Academic integration tests", freshlyCreated.Version), audit);
        if (activate.IsFailure)
        {
            throw new InvalidOperationException($"Seed activation failed: {activate.Error!.Code} - {activate.Error.Message}");
        }

        var identityUserId = created.Value.IdentityUserId ?? throw new InvalidOperationException("Seeded Student has no backing Identity User.");
        var login = await ResetPasswordAndLoginAsync(fixture, client, identityUserId, "a-strong-p@ssw0rd");
        return (created.Value.StudentId, login.AccessToken);
    }

    private static async Task<UMS.Modules.Identity.Application.Auth.TokenPairResult> ResetPasswordAndLoginAsync(AcademicApiFixture fixture, HttpClient client, Guid identityUserId, string newPassword)
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
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"POST {path} failed with {response.StatusCode}: {errorBody}");
        }

        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
}
