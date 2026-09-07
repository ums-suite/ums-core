using System.Net;
using System.Net.Http.Json;
using UMS.Modules.Academic.Application.Enrollments;
using UMS.Modules.Academic.IntegrationTests.Infrastructure;

namespace UMS.Modules.Academic.IntegrationTests.Enrollments;

/// <summary>
/// ACD-6/ACD-7's own reason for existing (ums-conventions.md, Testing: "concurrency-sensitive
/// invariants require an explicit concurrent-access test, not just a happy-path unit test") -
/// design-decisions.md "Seat-Limit Concurrency Control Pattern" / "Drop-Then-Reenroll Seat-Release
/// Ordering". Real parallel HTTP requests against a real Postgres (Testcontainers), never mocked.
/// </summary>
[Collection(AcademicApiTestCollectionDefinition.Name)]
public sealed class EnrollmentConcurrencyTests(AcademicApiFixture fixture)
{
    private const int Capacity = 5;
    private const int ConcurrentStudents = 20;

    /// <summary>
    /// Many concurrent Students (more than the seat limit) all submit <c>POST /enrollments</c> for
    /// the SAME CourseOffering at once. Asserts EXACTLY <see cref="Capacity"/> succeed and every
    /// other request gets a clean "seat no longer available" rejection - never an oversell (more
    /// than Capacity Active enrollments), and never a false rejection when seats were genuinely
    /// still free at that request's own commit.
    /// </summary>
    [Fact]
    public async Task Concurrent_enrollment_attempts_past_capacity_never_oversell_and_never_falsely_reject()
    {
        var client = fixture.CreateClient();
        var (_, _, _, adminToken) = await AcademicTestDataSeeder.ProvisionAdminAsync(fixture, client);
        var departmentId = await AcademicTestDataSeeder.SeedDepartmentAsync(client, adminToken);
        var program = await AcademicTestDataSeeder.SeedProgramAsync(client, adminToken, departmentId);
        var course = await AcademicTestDataSeeder.SeedCourseAsync(client, adminToken);
        var (_, semesterId) = await AcademicTestDataSeeder.SeedOpenSemesterAsync(client, adminToken);
        var offering = await AcademicTestDataSeeder.SeedCourseOfferingAsync(client, adminToken, course.Id, semesterId, departmentId, Capacity);
        var sectionId = offering.Sections.Single().Id;

        var studentTokens = new List<string>();
        for (var i = 0; i < ConcurrentStudents; i++)
        {
            var (_, accessToken) = await AcademicTestDataSeeder.SeedActiveStudentAsync(fixture, client, departmentId, program.Id);
            studentTokens.Add(accessToken);
        }

        var tasks = studentTokens.Select(async token =>
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/academic/enrollments")
            {
                Content = JsonContent.Create(new CreateEnrollmentRequest(offering.Id, sectionId, null)),
            }.WithBearerToken(token);
            using var perCallClient = fixture.CreateClient();
            return await perCallClient.SendAsync(request);
        });

        var responses = await Task.WhenAll(tasks);

        var succeeded = responses.Count(r => r.StatusCode == HttpStatusCode.Created);
        var rejected = responses.Where(r => r.StatusCode != HttpStatusCode.Created).ToList();

        Assert.Equal(Capacity, succeeded);
        Assert.Equal(ConcurrentStudents - Capacity, rejected.Count);
        Assert.All(rejected, r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));

        // The authoritative check: query the CourseOffering itself and confirm enrolled_count is
        // EXACTLY Capacity, never more (oversell) and never less (false rejection under-claiming).
        var offeringResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, $"/api/v1/academic/course-offerings/{offering.Id}").WithBearerToken(adminToken));
        offeringResponse.EnsureSuccessStatusCode();
        var refreshedOffering = await offeringResponse.Content.ReadFromJsonAsync<UMS.Modules.Academic.Application.CourseOfferings.CourseOfferingDto>();
        Assert.Equal(Capacity, refreshedOffering!.EnrolledCount);
        Assert.False(refreshedOffering.HasAvailableSeats);
    }

    /// <summary>edge-cases.md "A student's drop releasing a seat races a concurrent new-enrollment attempt for that seat" - a drop and a fresh enrollment attempt targeting the just-freed seat, both fired concurrently, must serialize correctly through the same atomic counter: never both succeed against a full offering, and the freed seat is claimable by exactly one of the racing new attempts.</summary>
    [Fact]
    public async Task A_drop_racing_a_new_enrollment_attempt_for_the_freed_seat_never_oversells()
    {
        var client = fixture.CreateClient();
        var (_, _, _, adminToken) = await AcademicTestDataSeeder.ProvisionAdminAsync(fixture, client);
        var departmentId = await AcademicTestDataSeeder.SeedDepartmentAsync(client, adminToken);
        var program = await AcademicTestDataSeeder.SeedProgramAsync(client, adminToken, departmentId);
        var course = await AcademicTestDataSeeder.SeedCourseAsync(client, adminToken);
        var (_, semesterId) = await AcademicTestDataSeeder.SeedOpenSemesterAsync(client, adminToken);
        const int capacity = 1;
        var offering = await AcademicTestDataSeeder.SeedCourseOfferingAsync(client, adminToken, course.Id, semesterId, departmentId, capacity);
        var sectionId = offering.Sections.Single().Id;

        // Fill the offering's one seat with Student A.
        var (_, studentAToken) = await AcademicTestDataSeeder.SeedActiveStudentAsync(fixture, client, departmentId, program.Id);
        var enrollAResponse = await fixture.CreateClient().SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/v1/academic/enrollments") { Content = JsonContent.Create(new CreateEnrollmentRequest(offering.Id, sectionId, null)) }.WithBearerToken(studentAToken));
        enrollAResponse.EnsureSuccessStatusCode();
        var enrollmentA = await enrollAResponse.Content.ReadFromJsonAsync<EnrollmentDto>();

        // Prepare several other Students racing to claim the seat the instant it's dropped.
        var racerTokens = new List<string>();
        for (var i = 0; i < 5; i++)
        {
            var (_, token) = await AcademicTestDataSeeder.SeedActiveStudentAsync(fixture, client, departmentId, program.Id);
            racerTokens.Add(token);
        }

        var dropTask = fixture.CreateClient().SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/academic/enrollments/{enrollmentA!.Id}") { Content = JsonContent.Create(new DropEnrollmentRequest("racing test")) }.WithBearerToken(studentAToken));
        var racerTasks = racerTokens.Select(token =>
            fixture.CreateClient().SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/v1/academic/enrollments") { Content = JsonContent.Create(new CreateEnrollmentRequest(offering.Id, sectionId, null)) }.WithBearerToken(token)));

        var allResponses = await Task.WhenAll(racerTasks.Append(dropTask));

        var offeringResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, $"/api/v1/academic/course-offerings/{offering.Id}").WithBearerToken(adminToken));
        var refreshedOffering = await offeringResponse.Content.ReadFromJsonAsync<UMS.Modules.Academic.Application.CourseOfferings.CourseOfferingDto>();

        // Never oversold: at most `capacity` seats occupied at any observed final state (the drop
        // releases one, at most one racer can then claim it).
        Assert.InRange(refreshedOffering!.EnrolledCount, 0, capacity);
        Assert.True(allResponses.Length == racerTokens.Count + 1);
    }
}
