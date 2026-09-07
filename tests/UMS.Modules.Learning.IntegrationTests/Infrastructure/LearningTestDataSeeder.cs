using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Academic.Application.AcademicSessions;
using UMS.Modules.Academic.Application.CourseOfferings;
using UMS.Modules.Academic.Application.Courses;
using UMS.Modules.Academic.Application.Enrollments;
using UMS.Modules.Academic.Application.Permissions;
using UMS.Modules.Academic.Application.Programs;
using UMS.Modules.Faculty.Application.FacultyMembers;
using UMS.Modules.Faculty.Application.Permissions;
using UMS.Modules.Identity.Application.Abstractions;
using UMS.Modules.Identity.Application.Users;
using UMS.Modules.Identity.Domain.Roles;
using UMS.Modules.Identity.Domain.Users;
using UMS.Modules.Learning.Application.Permissions;
using UMS.Modules.Organization.Application.Campuses;
using UMS.Modules.Organization.Application.Departments;
using UMS.Modules.Organization.Application.Designations;
using UMS.Modules.Organization.Application.Faculties;
using UMS.Modules.Organization.Application.Permissions;
using UMS.Modules.Organization.Application.Universities;
using UMS.Modules.Student.Application.Common;
using UMS.Modules.Student.Application.Students;
using UMS.Shared.Student;

namespace UMS.Modules.Learning.IntegrationTests.Infrastructure;

/// <summary>
/// Test-only bootstrap for everything Learning's own integration suite needs, built entirely
/// through the REAL cross-module endpoints and contracts Learning itself depends on: an admin User,
/// a real Organization chain, a real onboarded (login-capable) FacultyMember assigned as a real
/// <c>CourseOffering</c>'s Instructor, and real Active Students with real <c>Enrollment</c>s -
/// because Learning's own instructor-ownership and enrollment-scoping checks resolve exclusively
/// through <c>UMS.Shared.Academic.ICourseOfferingLookup</c>, which reads Academic's own real rows.
/// Mirrors <c>AcademicTestDataSeeder</c> exactly, extended for Learning's own surface.
/// </summary>
public static class LearningTestDataSeeder
{
    /// <summary>One fully-wired course context: an offering with a real assigned Instructor and two real enrolled Students, which is what every Learning test needs before it can do anything at all.</summary>
    public sealed record CourseContext(
        Guid DepartmentId,
        Guid ProgramId,
        Guid SemesterId,
        Guid CourseOfferingId,
        Guid SectionId,
        Guid InstructorFacultyMemberId,
        Guid InstructorUserId,
        string InstructorToken,
        Guid StudentId,
        string StudentToken,
        Guid SecondStudentId,
        string SecondStudentToken);

    public static async Task<(Guid UserId, string Username, string Password, string AccessToken)> ProvisionAdminAsync(LearningApiFixture fixture, HttpClient client)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        ArgumentNullException.ThrowIfNull(client);

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
                $"IntegrationTestLearningAdmin-{Guid.NewGuid():N}",
                "Full Identity/Organization/Faculty/Academic-permission Role used only by the integration test suite.",
                [
                    OrganizationPermissions.UniversityManage,
                    OrganizationPermissions.CampusManage,
                    OrganizationPermissions.FacultyManage,
                    OrganizationPermissions.DepartmentManage,
                    OrganizationPermissions.DesignationManage,
                    FacultyPermissions.MemberManage,
                    AcademicPermissions.ProgramManage,
                    AcademicPermissions.CourseManage,
                    AcademicPermissions.AcademicSessionManage,
                    AcademicPermissions.CourseOfferingManage,
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

    /// <summary>Builds the whole Organization -&gt; Academic -&gt; Faculty -&gt; Student chain Learning sits on top of, through each of those modules' own real endpoints.</summary>
    public static async Task<CourseContext> SeedCourseContextAsync(LearningApiFixture fixture, HttpClient client, string adminToken)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        ArgumentNullException.ThrowIfNull(client);

        var university = await PostAsync<UniversityDto>(client, adminToken, "/api/v1/organization/universities", new CreateUniversityRequest($"Test University {Guid.NewGuid():N}", null));
        var campus = await PostAsync<CampusDto>(client, adminToken, "/api/v1/organization/campuses", new CreateCampusRequest(university.Id, $"Main Campus {Guid.NewGuid():N}"));
        var orgFaculty = await PostAsync<FacultyDto>(client, adminToken, "/api/v1/organization/faculties", new CreateFacultyRequest(campus.Id, $"Faculty of Testing {Guid.NewGuid():N}", null));
        var department = await PostAsync<DepartmentDto>(client, adminToken, "/api/v1/organization/departments", new CreateDepartmentRequest(orgFaculty.Id, $"Department of Testing {Guid.NewGuid():N}", null));
        var designation = await PostAsync<DesignationDto>(client, adminToken, "/api/v1/organization/designations", new CreateDesignationRequest($"Lecturer {Guid.NewGuid():N}", null));

        var program = await PostAsync<ProgramDto>(client, adminToken, "/api/v1/academic/programs", new CreateProgramRequest(department.Id, $"PROG-{Guid.NewGuid():N}"[..12], "Test Program", 15, false));
        var course = await PostAsync<CourseDto>(client, adminToken, "/api/v1/academic/courses", new CreateCourseRequest($"CRS-{Guid.NewGuid():N}"[..10], "Test Course", 3, null));

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var year = Interlocked.Increment(ref _nextAcademicSessionYear);
        var session = await PostAsync<AcademicSessionDto>(
            client,
            adminToken,
            "/api/v1/academic/academic-sessions",
            new CreateAcademicSessionRequest($"{year}-{year + 1}", [new CreateSemesterRequest("Test Semester", today.AddDays(-1), today.AddDays(60), today.AddDays(-1), today.AddDays(60))]));
        var semesterId = session.Semesters.Single().Id;

        var offering = await PostAsync<CourseOfferingDto>(
            client,
            adminToken,
            "/api/v1/academic/course-offerings",
            new CreateCourseOfferingRequest(course.Id, semesterId, department.Id, 10, [new CreateSectionRequest("SEC-A", DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(10, 0))]));
        var sectionId = offering.Sections.Single().Id;

        var (facultyMemberId, instructorUserId, instructorToken) = await SeedInstructorAsync(fixture, client, adminToken, department.Id, designation.Id);
        (await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, $"/api/v1/academic/course-offerings/{offering.Id}/instructor")
        {
            Content = JsonContent.Create(new AssignInstructorRequest(facultyMemberId)),
        }.WithBearerToken(adminToken))).EnsureSuccessStatusCode();

        var (studentId, studentToken) = await SeedEnrolledStudentAsync(fixture, client, department.Id, program.Id, offering.Id, sectionId);
        var (secondStudentId, secondStudentToken) = await SeedEnrolledStudentAsync(fixture, client, department.Id, program.Id, offering.Id, sectionId);

        return new CourseContext(
            department.Id,
            program.Id,
            semesterId,
            offering.Id,
            sectionId,
            facultyMemberId,
            instructorUserId,
            instructorToken,
            studentId,
            studentToken,
            secondStudentId,
            secondStudentToken);
    }

    /// <summary>Onboards a second, unrelated Instructor (a different Department entirely) - what the "another Instructor may not touch my Assignment" ownership tests are driven with.</summary>
    public static async Task<string> SeedUnrelatedInstructorTokenAsync(LearningApiFixture fixture, HttpClient client, string adminToken)
    {
        ArgumentNullException.ThrowIfNull(client);

        var university = await PostAsync<UniversityDto>(client, adminToken, "/api/v1/organization/universities", new CreateUniversityRequest($"Other University {Guid.NewGuid():N}", null));
        var campus = await PostAsync<CampusDto>(client, adminToken, "/api/v1/organization/campuses", new CreateCampusRequest(university.Id, $"Other Campus {Guid.NewGuid():N}"));
        var orgFaculty = await PostAsync<FacultyDto>(client, adminToken, "/api/v1/organization/faculties", new CreateFacultyRequest(campus.Id, $"Other Faculty {Guid.NewGuid():N}", null));
        var department = await PostAsync<DepartmentDto>(client, adminToken, "/api/v1/organization/departments", new CreateDepartmentRequest(orgFaculty.Id, $"Other Department {Guid.NewGuid():N}", null));
        var designation = await PostAsync<DesignationDto>(client, adminToken, "/api/v1/organization/designations", new CreateDesignationRequest($"Other Lecturer {Guid.NewGuid():N}", null));

        var (_, _, token) = await SeedInstructorAsync(fixture, client, adminToken, department.Id, designation.Id);
        return token;
    }

    /// <summary>A real, login-capable Student who is NOT enrolled in the seeded CourseOffering - what the enrollment-scoping tests are driven with.</summary>
    public static async Task<string> SeedUnenrolledStudentTokenAsync(LearningApiFixture fixture, HttpClient client, Guid departmentId, Guid programId)
    {
        var (_, token) = await SeedStudentAsync(fixture, client, departmentId, programId);
        return token;
    }

    private static int _nextAcademicSessionYear = 2100 + Random.Shared.Next(0, 3000);

    private static async Task<(Guid FacultyMemberId, Guid UserId, string AccessToken)> SeedInstructorAsync(
        LearningApiFixture fixture,
        HttpClient client,
        string adminToken,
        Guid departmentId,
        Guid designationId)
    {
        var user = await TestUsers.ProvisionAsync(client);
        var facultyMember = await PostAsync<FacultyMemberDto>(
            client,
            adminToken,
            "/api/v1/faculty/members",
            new OnboardFacultyMemberRequest(user.Id, $"EMP-{Guid.NewGuid():N}"[..10], departmentId, designationId, "FullTime", DateOnly.FromDateTime(DateTime.UtcNow)));

        await AssignRoleAsync(
            fixture,
            user.Id,
            $"IntegrationTestLearningInstructor-{Guid.NewGuid():N}",
            [
                LearningPermissions.AssignmentManage,
                LearningPermissions.AssignmentExtensionGrant,
                LearningPermissions.SubmissionReadBatch,
                LearningPermissions.SubmissionEvaluate,
                LearningPermissions.LectureMaterialManage,
                LearningPermissions.DiscussionModerate,
            ]);

        var login = await TestUsers.LoginAsync(client, user.Username);
        return (facultyMember.Id, user.Id, login.AccessToken);
    }

    private static async Task<(Guid StudentId, string AccessToken)> SeedEnrolledStudentAsync(
        LearningApiFixture fixture,
        HttpClient client,
        Guid departmentId,
        Guid programId,
        Guid courseOfferingId,
        Guid sectionId)
    {
        var (studentId, token) = await SeedStudentAsync(fixture, client, departmentId, programId);

        var enrollResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/v1/academic/enrollments")
        {
            Content = JsonContent.Create(new CreateEnrollmentRequest(courseOfferingId, sectionId, null)),
        }.WithBearerToken(token));
        enrollResponse.EnsureSuccessStatusCode();

        return (studentId, token);
    }

    private static async Task<(Guid StudentId, string AccessToken)> SeedStudentAsync(LearningApiFixture fixture, HttpClient client, Guid departmentId, Guid programId)
    {
        Guid studentId;
        Guid identityUserId;

        using (var scope = fixture.Services.CreateScope())
        {
            var provisioner = scope.ServiceProvider.GetRequiredService<IStudentRecordProvisioner>();
            var statusService = scope.ServiceProvider.GetRequiredService<StudentStatusService>();
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
            var activate = await statusService.ChangeStatusAsync(
                created.Value.StudentId,
                Guid.NewGuid(),
                new ChangeStudentStatusRequest("Active", "Seeded Active for Learning integration tests", freshlyCreated.Version),
                audit);
            if (activate.IsFailure)
            {
                throw new InvalidOperationException($"Seed activation failed: {activate.Error!.Code} - {activate.Error.Message}");
            }

            studentId = created.Value.StudentId;
            identityUserId = created.Value.IdentityUserId ?? throw new InvalidOperationException("Seeded Student has no backing Identity User.");
        }

        var login = await ResetPasswordAndLoginAsync(fixture, client, identityUserId, "a-strong-p@ssw0rd");
        return (studentId, login.AccessToken);
    }

    private static async Task AssignRoleAsync(LearningApiFixture fixture, Guid userId, string roleName, IReadOnlyCollection<string> permissions)
    {
        using var scope = fixture.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var roles = scope.ServiceProvider.GetRequiredService<IRoleRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var now = clock.UtcNow;
        var role = Role.Create(roleName, "Permission bundle used only by the integration test suite.", permissions, now);
        roles.Add(role);

        var domainUser = await users.GetByIdAsync(new UserId(userId)) ?? throw new InvalidOperationException($"User '{userId}' was not found.");
        domainUser.AssignRole(role.Id, scopeNode: null, now);

        await unitOfWork.SaveChangesAsync();
    }

    private static async Task<UMS.Modules.Identity.Application.Auth.TokenPairResult> ResetPasswordAndLoginAsync(LearningApiFixture fixture, HttpClient client, Guid identityUserId, string newPassword)
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
