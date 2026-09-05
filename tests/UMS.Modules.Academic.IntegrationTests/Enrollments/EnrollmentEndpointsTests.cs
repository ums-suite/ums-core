using System.Net;
using System.Net.Http.Json;
using UMS.Modules.Academic.Application.Courses;
using UMS.Modules.Academic.Application.Enrollments;
using UMS.Modules.Academic.IntegrationTests.Infrastructure;

namespace UMS.Modules.Academic.IntegrationTests.Enrollments;

/// <summary>ACD-6/ACD-7/ACD-8: the atomic 4-check enrollment gate, drop, and advisor approval - each check exercised independently (ums-conventions.md, Testing).</summary>
[Collection(AcademicApiTestCollectionDefinition.Name)]
public sealed class EnrollmentEndpointsTests(AcademicApiFixture fixture)
{
    [Fact]
    public async Task Create_succeeds_when_all_four_checks_pass()
    {
        var client = fixture.CreateClient();
        var (_, _, _, adminToken) = await AcademicTestDataSeeder.ProvisionAdminAsync(fixture, client);
        var departmentId = await AcademicTestDataSeeder.SeedDepartmentAsync(client, adminToken);
        var program = await AcademicTestDataSeeder.SeedProgramAsync(client, adminToken, departmentId);
        var course = await AcademicTestDataSeeder.SeedCourseAsync(client, adminToken);
        var (_, semesterId) = await AcademicTestDataSeeder.SeedOpenSemesterAsync(client, adminToken);
        var offering = await AcademicTestDataSeeder.SeedCourseOfferingAsync(client, adminToken, course.Id, semesterId, departmentId, capacity: 5);
        var (_, studentToken) = await AcademicTestDataSeeder.SeedActiveStudentAsync(fixture, client, departmentId, program.Id);

        var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/v1/academic/enrollments")
        {
            Content = JsonContent.Create(new CreateEnrollmentRequest(offering.Id, offering.Sections.Single().Id, null)),
        }.WithBearerToken(studentToken));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var enrollment = await response.Content.ReadFromJsonAsync<EnrollmentDto>();
        Assert.Equal("Active", enrollment!.Status);
    }

    [Fact]
    public async Task Create_is_rejected_when_the_prerequisite_gate_fails()
    {
        var client = fixture.CreateClient();
        var (_, _, _, adminToken) = await AcademicTestDataSeeder.ProvisionAdminAsync(fixture, client);
        var departmentId = await AcademicTestDataSeeder.SeedDepartmentAsync(client, adminToken);
        var program = await AcademicTestDataSeeder.SeedProgramAsync(client, adminToken, departmentId);
        var prerequisiteCourse = await AcademicTestDataSeeder.SeedCourseAsync(client, adminToken);
        var course = await AcademicTestDataSeeder.SeedCourseAsync(client, adminToken, prerequisiteCourseIds: [prerequisiteCourse.Id]);
        var (_, semesterId) = await AcademicTestDataSeeder.SeedOpenSemesterAsync(client, adminToken);
        var offering = await AcademicTestDataSeeder.SeedCourseOfferingAsync(client, adminToken, course.Id, semesterId, departmentId, capacity: 5);
        var (_, studentToken) = await AcademicTestDataSeeder.SeedActiveStudentAsync(fixture, client, departmentId, program.Id);

        var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/v1/academic/enrollments")
        {
            Content = JsonContent.Create(new CreateEnrollmentRequest(offering.Id, offering.Sections.Single().Id, null)),
        }.WithBearerToken(studentToken));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("prerequisite_not_met", body);
    }

    [Fact]
    public async Task Create_with_an_explicit_prerequisite_override_reason_bypasses_the_prerequisite_gate()
    {
        var client = fixture.CreateClient();
        var (_, _, _, adminToken) = await AcademicTestDataSeeder.ProvisionAdminAsync(fixture, client);
        var departmentId = await AcademicTestDataSeeder.SeedDepartmentAsync(client, adminToken);
        var program = await AcademicTestDataSeeder.SeedProgramAsync(client, adminToken, departmentId);
        var prerequisiteCourse = await AcademicTestDataSeeder.SeedCourseAsync(client, adminToken);
        var course = await AcademicTestDataSeeder.SeedCourseAsync(client, adminToken, prerequisiteCourseIds: [prerequisiteCourse.Id]);
        var (_, semesterId) = await AcademicTestDataSeeder.SeedOpenSemesterAsync(client, adminToken);
        var offering = await AcademicTestDataSeeder.SeedCourseOfferingAsync(client, adminToken, course.Id, semesterId, departmentId, capacity: 5);
        var (_, studentToken) = await AcademicTestDataSeeder.SeedActiveStudentAsync(fixture, client, departmentId, program.Id);

        var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/v1/academic/enrollments")
        {
            Content = JsonContent.Create(new CreateEnrollmentRequest(offering.Id, offering.Sections.Single().Id, "Advisor-approved override: equivalent transfer credit")),
        }.WithBearerToken(studentToken));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var enrollment = await response.Content.ReadFromJsonAsync<EnrollmentDto>();
        Assert.NotNull(enrollment!.PrerequisiteOverrideReason);
    }

    [Fact]
    public async Task Create_is_rejected_when_it_would_exceed_the_Programs_credit_limit()
    {
        var client = fixture.CreateClient();
        var (_, _, _, adminToken) = await AcademicTestDataSeeder.ProvisionAdminAsync(fixture, client);
        var departmentId = await AcademicTestDataSeeder.SeedDepartmentAsync(client, adminToken);
        var program = await AcademicTestDataSeeder.SeedProgramAsync(client, adminToken, departmentId, maxCreditsPerSemester: 3);
        var course = await AcademicTestDataSeeder.SeedCourseAsync(client, adminToken, creditHours: 4);
        var (_, semesterId) = await AcademicTestDataSeeder.SeedOpenSemesterAsync(client, adminToken);
        var offering = await AcademicTestDataSeeder.SeedCourseOfferingAsync(client, adminToken, course.Id, semesterId, departmentId, capacity: 5);
        var (_, studentToken) = await AcademicTestDataSeeder.SeedActiveStudentAsync(fixture, client, departmentId, program.Id);

        var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/v1/academic/enrollments")
        {
            Content = JsonContent.Create(new CreateEnrollmentRequest(offering.Id, offering.Sections.Single().Id, null)),
        }.WithBearerToken(studentToken));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("credit_limit_exceeded", body);
    }

    [Fact]
    public async Task Create_is_rejected_when_the_chosen_Section_conflicts_with_an_existing_Active_enrollments_timetable()
    {
        var client = fixture.CreateClient();
        var (_, _, _, adminToken) = await AcademicTestDataSeeder.ProvisionAdminAsync(fixture, client);
        var departmentId = await AcademicTestDataSeeder.SeedDepartmentAsync(client, adminToken);
        var program = await AcademicTestDataSeeder.SeedProgramAsync(client, adminToken, departmentId, maxCreditsPerSemester: 30);
        var courseOne = await AcademicTestDataSeeder.SeedCourseAsync(client, adminToken);
        var courseTwo = await AcademicTestDataSeeder.SeedCourseAsync(client, adminToken);
        var (_, semesterId) = await AcademicTestDataSeeder.SeedOpenSemesterAsync(client, adminToken);

        // Both offerings' one Section shares the identical Monday 9-10 slot (SeedCourseOfferingAsync's own default).
        var offeringOne = await AcademicTestDataSeeder.SeedCourseOfferingAsync(client, adminToken, courseOne.Id, semesterId, departmentId, capacity: 5);
        var offeringTwo = await AcademicTestDataSeeder.SeedCourseOfferingAsync(client, adminToken, courseTwo.Id, semesterId, departmentId, capacity: 5);
        var (_, studentToken) = await AcademicTestDataSeeder.SeedActiveStudentAsync(fixture, client, departmentId, program.Id);

        var firstResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/v1/academic/enrollments") { Content = JsonContent.Create(new CreateEnrollmentRequest(offeringOne.Id, offeringOne.Sections.Single().Id, null)) }.WithBearerToken(studentToken));
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        var secondResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/v1/academic/enrollments") { Content = JsonContent.Create(new CreateEnrollmentRequest(offeringTwo.Id, offeringTwo.Sections.Single().Id, null)) }.WithBearerToken(studentToken));

        Assert.Equal(HttpStatusCode.BadRequest, secondResponse.StatusCode);
        var body = await secondResponse.Content.ReadAsStringAsync();
        Assert.Contains("timetable_conflict", body);
    }

    [Fact]
    public async Task A_retried_duplicate_Create_for_the_same_Student_CourseOffering_Semester_is_idempotent_not_a_second_row()
    {
        var client = fixture.CreateClient();
        var (_, _, _, adminToken) = await AcademicTestDataSeeder.ProvisionAdminAsync(fixture, client);
        var departmentId = await AcademicTestDataSeeder.SeedDepartmentAsync(client, adminToken);
        var program = await AcademicTestDataSeeder.SeedProgramAsync(client, adminToken, departmentId);
        var course = await AcademicTestDataSeeder.SeedCourseAsync(client, adminToken);
        var (_, semesterId) = await AcademicTestDataSeeder.SeedOpenSemesterAsync(client, adminToken);
        var offering = await AcademicTestDataSeeder.SeedCourseOfferingAsync(client, adminToken, course.Id, semesterId, departmentId, capacity: 5);
        var (_, studentToken) = await AcademicTestDataSeeder.SeedActiveStudentAsync(fixture, client, departmentId, program.Id);
        var request = new CreateEnrollmentRequest(offering.Id, offering.Sections.Single().Id, null);

        var first = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/v1/academic/enrollments") { Content = JsonContent.Create(request) }.WithBearerToken(studentToken));
        var second = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/v1/academic/enrollments") { Content = JsonContent.Create(request) }.WithBearerToken(studentToken));

        var firstBody = await first.Content.ReadAsStringAsync();
        var secondBody = await second.Content.ReadAsStringAsync();
        Assert.True(first.IsSuccessStatusCode, $"first: {first.StatusCode} {firstBody}");
        Assert.True(second.IsSuccessStatusCode, $"second: {second.StatusCode} {secondBody}");

        var firstEnrollment = await first.Content.ReadFromJsonAsync<EnrollmentDto>();
        var secondEnrollment = await second.Content.ReadFromJsonAsync<EnrollmentDto>();
        Assert.Equal(firstEnrollment!.Id, secondEnrollment!.Id);

        var offeringResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, $"/api/v1/academic/course-offerings/{offering.Id}").WithBearerToken(adminToken));
        var refreshedOffering = await offeringResponse.Content.ReadFromJsonAsync<UMS.Modules.Academic.Application.CourseOfferings.CourseOfferingDto>();
        Assert.Equal(1, refreshedOffering!.EnrolledCount);
    }

    [Fact]
    public async Task Drop_releases_the_seat_and_a_subsequent_new_enrollment_can_claim_it()
    {
        var client = fixture.CreateClient();
        var (_, _, _, adminToken) = await AcademicTestDataSeeder.ProvisionAdminAsync(fixture, client);
        var departmentId = await AcademicTestDataSeeder.SeedDepartmentAsync(client, adminToken);
        var program = await AcademicTestDataSeeder.SeedProgramAsync(client, adminToken, departmentId);
        var course = await AcademicTestDataSeeder.SeedCourseAsync(client, adminToken);
        var (_, semesterId) = await AcademicTestDataSeeder.SeedOpenSemesterAsync(client, adminToken);
        var offering = await AcademicTestDataSeeder.SeedCourseOfferingAsync(client, adminToken, course.Id, semesterId, departmentId, capacity: 1);
        var (_, studentAToken) = await AcademicTestDataSeeder.SeedActiveStudentAsync(fixture, client, departmentId, program.Id);
        var (_, studentBToken) = await AcademicTestDataSeeder.SeedActiveStudentAsync(fixture, client, departmentId, program.Id);
        var sectionId = offering.Sections.Single().Id;

        var enrollAResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/v1/academic/enrollments") { Content = JsonContent.Create(new CreateEnrollmentRequest(offering.Id, sectionId, null)) }.WithBearerToken(studentAToken));
        var enrollmentA = await enrollAResponse.Content.ReadFromJsonAsync<EnrollmentDto>();

        // Full offering: Student B is rejected.
        var rejectedResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/v1/academic/enrollments") { Content = JsonContent.Create(new CreateEnrollmentRequest(offering.Id, sectionId, null)) }.WithBearerToken(studentBToken));
        Assert.Equal(HttpStatusCode.Conflict, rejectedResponse.StatusCode);

        var dropResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/academic/enrollments/{enrollmentA!.Id}") { Content = JsonContent.Create(new DropEnrollmentRequest("changed my mind")) }.WithBearerToken(studentAToken));
        Assert.Equal(HttpStatusCode.OK, dropResponse.StatusCode);

        var claimResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/v1/academic/enrollments") { Content = JsonContent.Create(new CreateEnrollmentRequest(offering.Id, sectionId, null)) }.WithBearerToken(studentBToken));
        Assert.Equal(HttpStatusCode.Created, claimResponse.StatusCode);
    }

    [Fact]
    public async Task A_retried_duplicate_Create_against_an_offering_the_same_students_own_enrollment_filled_to_capacity_is_idempotent_not_a_conflict()
    {
        // Regression test for a genuine bug caught during this flow's manual end-to-end
        // verification: the pre-fix implementation only recognized a duplicate/double-click
        // resubmission via a unique-constraint-violation catch AFTER the seat-limit claim - so
        // whenever the offering's only remaining seat was the caller's own just-created Enrollment
        // (capacity: 1, exactly the scenario a real double-click on a popular, now-full offering
        // produces), the seat claim itself failed first and the request returned
        // "seat_no_longer_available" instead of the documented idempotent no-op.
        var client = fixture.CreateClient();
        var (_, _, _, adminToken) = await AcademicTestDataSeeder.ProvisionAdminAsync(fixture, client);
        var departmentId = await AcademicTestDataSeeder.SeedDepartmentAsync(client, adminToken);
        var program = await AcademicTestDataSeeder.SeedProgramAsync(client, adminToken, departmentId);
        var course = await AcademicTestDataSeeder.SeedCourseAsync(client, adminToken);
        var (_, semesterId) = await AcademicTestDataSeeder.SeedOpenSemesterAsync(client, adminToken);
        var offering = await AcademicTestDataSeeder.SeedCourseOfferingAsync(client, adminToken, course.Id, semesterId, departmentId, capacity: 1);
        var (_, studentToken) = await AcademicTestDataSeeder.SeedActiveStudentAsync(fixture, client, departmentId, program.Id);
        var request = new CreateEnrollmentRequest(offering.Id, offering.Sections.Single().Id, null);

        var first = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/v1/academic/enrollments") { Content = JsonContent.Create(request) }.WithBearerToken(studentToken));
        var second = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/v1/academic/enrollments") { Content = JsonContent.Create(request) }.WithBearerToken(studentToken));

        var secondBody = await second.Content.ReadAsStringAsync();
        Assert.True(second.IsSuccessStatusCode, $"second: {second.StatusCode} {secondBody}");

        var firstEnrollment = await first.Content.ReadFromJsonAsync<EnrollmentDto>();
        var secondEnrollment = await second.Content.ReadFromJsonAsync<EnrollmentDto>();
        Assert.Equal(firstEnrollment!.Id, secondEnrollment!.Id);
        Assert.Equal("Active", secondEnrollment.Status);
    }

    [Fact]
    public async Task Create_after_dropping_the_same_CourseOffering_creates_a_fresh_Active_enrollment_not_the_stale_Dropped_one()
    {
        // Regression test for a genuine bug caught during this flow's manual end-to-end
        // verification: the pre-fix unique constraint on (student, courseOffering, semester) was
        // unconditional, so once a Student dropped a CourseOffering, EVERY later Create attempt for
        // that exact tuple collided with the now-Dropped row and the duplicate-value catch path
        // handed back that stale Dropped DTO forever - a real drop-then-re-enroll-in-the-same-
        // offering was permanently impossible after the first drop.
        var client = fixture.CreateClient();
        var (_, _, _, adminToken) = await AcademicTestDataSeeder.ProvisionAdminAsync(fixture, client);
        var departmentId = await AcademicTestDataSeeder.SeedDepartmentAsync(client, adminToken);
        var program = await AcademicTestDataSeeder.SeedProgramAsync(client, adminToken, departmentId);
        var course = await AcademicTestDataSeeder.SeedCourseAsync(client, adminToken);
        var (_, semesterId) = await AcademicTestDataSeeder.SeedOpenSemesterAsync(client, adminToken);
        var offering = await AcademicTestDataSeeder.SeedCourseOfferingAsync(client, adminToken, course.Id, semesterId, departmentId, capacity: 5);
        var (_, studentToken) = await AcademicTestDataSeeder.SeedActiveStudentAsync(fixture, client, departmentId, program.Id);
        var sectionId = offering.Sections.Single().Id;

        var enrollResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/v1/academic/enrollments") { Content = JsonContent.Create(new CreateEnrollmentRequest(offering.Id, sectionId, null)) }.WithBearerToken(studentToken));
        var enrollment = await enrollResponse.Content.ReadFromJsonAsync<EnrollmentDto>();

        var dropResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/academic/enrollments/{enrollment!.Id}") { Content = JsonContent.Create(new DropEnrollmentRequest("changed my mind")) }.WithBearerToken(studentToken));
        Assert.Equal(HttpStatusCode.OK, dropResponse.StatusCode);

        var reenrollResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/v1/academic/enrollments") { Content = JsonContent.Create(new CreateEnrollmentRequest(offering.Id, sectionId, null)) }.WithBearerToken(studentToken));
        var reenrollBody = await reenrollResponse.Content.ReadAsStringAsync();
        Assert.True(reenrollResponse.IsSuccessStatusCode, $"reenroll: {reenrollResponse.StatusCode} {reenrollBody}");

        var reenrollment = await reenrollResponse.Content.ReadFromJsonAsync<EnrollmentDto>();
        Assert.NotEqual(enrollment.Id, reenrollment!.Id);
        Assert.Equal("Active", reenrollment.Status);
    }

    [Fact]
    public async Task Advisor_approval_gate_holds_a_Program_configured_Enrollment_Pending_until_explicitly_approved()
    {
        var client = fixture.CreateClient();
        var (_, _, _, adminToken) = await AcademicTestDataSeeder.ProvisionAdminAsync(fixture, client);
        var departmentId = await AcademicTestDataSeeder.SeedDepartmentAsync(client, adminToken);
        var program = await AcademicTestDataSeeder.SeedProgramAsync(client, adminToken, departmentId, requiresAdvisorApproval: true);
        var course = await AcademicTestDataSeeder.SeedCourseAsync(client, adminToken);
        var (_, semesterId) = await AcademicTestDataSeeder.SeedOpenSemesterAsync(client, adminToken);
        var offering = await AcademicTestDataSeeder.SeedCourseOfferingAsync(client, adminToken, course.Id, semesterId, departmentId, capacity: 5);
        var (_, studentToken) = await AcademicTestDataSeeder.SeedActiveStudentAsync(fixture, client, departmentId, program.Id);

        var createResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/v1/academic/enrollments") { Content = JsonContent.Create(new CreateEnrollmentRequest(offering.Id, offering.Sections.Single().Id, null)) }.WithBearerToken(studentToken));
        var enrollment = await createResponse.Content.ReadFromJsonAsync<EnrollmentDto>();
        Assert.Equal("Pending", enrollment!.Status);

        var approveResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, $"/api/v1/academic/enrollments/{enrollment.Id}/approve").WithBearerToken(adminToken));
        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);
        var approved = await approveResponse.Content.ReadFromJsonAsync<EnrollmentDto>();
        Assert.Equal("Active", approved!.Status);
    }
}
