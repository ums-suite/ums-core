using System.Net;
using System.Net.Http.Json;
using UMS.Modules.Academic.Application.Attendance;
using UMS.Modules.Academic.Application.CourseOfferings;
using UMS.Modules.Academic.Application.Enrollments;
using UMS.Modules.Academic.IntegrationTests.Infrastructure;

namespace UMS.Modules.Academic.IntegrationTests.Attendance;

/// <summary>ACD-9: per-session attendance, the assigned-instructor-only gate (via a fresh Faculty lookup), and edge-cases.md "Attendance correction-window-close racing a late marking attempt".</summary>
[Collection(AcademicApiTestCollectionDefinition.Name)]
public sealed class AttendanceEndpointsTests(AcademicApiFixture fixture)
{
    private async Task<(HttpClient Client, string AdminToken, string InstructorToken, Guid CourseOfferingId, Guid EnrollmentId)> SeedAsync()
    {
        var client = fixture.CreateClient();
        var (_, _, _, adminToken) = await AcademicTestDataSeeder.ProvisionAdminAsync(fixture, client);
        var departmentId = await AcademicTestDataSeeder.SeedDepartmentAsync(client, adminToken);
        var designationId = await AcademicTestDataSeeder.SeedDesignationAsync(client, adminToken);
        var program = await AcademicTestDataSeeder.SeedProgramAsync(client, adminToken, departmentId);
        var course = await AcademicTestDataSeeder.SeedCourseAsync(client, adminToken);
        var (_, semesterId) = await AcademicTestDataSeeder.SeedOpenSemesterAsync(client, adminToken);
        var offering = await AcademicTestDataSeeder.SeedCourseOfferingAsync(client, adminToken, course.Id, semesterId, departmentId, capacity: 5);
        var (facultyMemberId, _, instructorToken) = await AcademicTestDataSeeder.SeedFacultyMemberAsync(fixture, client, adminToken, departmentId, designationId);
        (await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, $"/api/v1/academic/course-offerings/{offering.Id}/instructor") { Content = JsonContent.Create(new AssignInstructorRequest(facultyMemberId)) }.WithBearerToken(adminToken))).EnsureSuccessStatusCode();

        var (_, studentToken) = await AcademicTestDataSeeder.SeedActiveStudentAsync(fixture, client, departmentId, program.Id);
        var enrollResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/v1/academic/enrollments") { Content = JsonContent.Create(new CreateEnrollmentRequest(offering.Id, offering.Sections.Single().Id, null)) }.WithBearerToken(studentToken));
        enrollResponse.EnsureSuccessStatusCode();
        var enrollment = await enrollResponse.Content.ReadFromJsonAsync<EnrollmentDto>();

        return (client, adminToken, instructorToken, offering.Id, enrollment!.Id);
    }

    [Fact]
    public async Task The_assigned_instructor_can_mark_attendance_for_their_own_CourseOffering()
    {
        var (client, _, instructorToken, courseOfferingId, enrollmentId) = await SeedAsync();

        var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/v1/academic/attendance")
        {
            Content = JsonContent.Create(new MarkAttendanceRequest(courseOfferingId, DateOnly.FromDateTime(DateTime.UtcNow), null, enrollmentId, "Present")),
        }.WithBearerToken(instructorToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var session = await response.Content.ReadFromJsonAsync<AttendanceSessionDto>();
        Assert.Single(session!.Records);
        Assert.Equal("Present", session.Records.Single().Status);
    }

    /// <summary>design-decisions.md "In-Process Event Delivery Guarantee for InstructorAssigned Projections" - a FacultyMember who is NOT the assigned instructor is rejected, resolved via a fresh synchronous lookup on every call.</summary>
    [Fact]
    public async Task A_FacultyMember_who_is_not_the_assigned_instructor_is_rejected()
    {
        var (client, adminToken, _, courseOfferingId, enrollmentId) = await SeedAsync();
        var departmentId = await AcademicTestDataSeeder.SeedDepartmentAsync(client, adminToken);
        var designationId = await AcademicTestDataSeeder.SeedDesignationAsync(client, adminToken);
        var (_, _, otherFacultyToken) = await AcademicTestDataSeeder.SeedFacultyMemberAsync(fixture, client, adminToken, departmentId, designationId);

        var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/v1/academic/attendance")
        {
            Content = JsonContent.Create(new MarkAttendanceRequest(courseOfferingId, DateOnly.FromDateTime(DateTime.UtcNow), null, enrollmentId, "Present")),
        }.WithBearerToken(otherFacultyToken));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Marking_attendance_after_the_configured_correction_window_has_closed_is_rejected_outright()
    {
        var (client, _, instructorToken, courseOfferingId, enrollmentId) = await SeedAsync();

        var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/v1/academic/attendance")
        {
            Content = JsonContent.Create(new MarkAttendanceRequest(courseOfferingId, DateOnly.FromDateTime(DateTime.UtcNow), DateTimeOffset.UtcNow.AddMinutes(-5), enrollmentId, "Present")),
        }.WithBearerToken(instructorToken));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("correction_window_closed", body);
    }
}
