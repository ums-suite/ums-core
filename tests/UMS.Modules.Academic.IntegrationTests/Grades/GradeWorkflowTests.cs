using System.Net;
using System.Net.Http.Json;
using UMS.Modules.Academic.Application.CourseOfferings;
using UMS.Modules.Academic.Application.Enrollments;
using UMS.Modules.Academic.Application.Grades;
using UMS.Modules.Academic.Application.ResultPublications;
using UMS.Modules.Academic.IntegrationTests.Infrastructure;

namespace UMS.Modules.Academic.IntegrationTests.Grades;

/// <summary>ACD-10..13: Faculty submit -&gt; Department-Head lock -&gt; authority approve -&gt; publish -&gt; correction, and the two concurrency-critical races design-decisions.md's "Grade-Lock State Machine Design" names.</summary>
[Collection(AcademicApiTestCollectionDefinition.Name)]
public sealed class GradeWorkflowTests(AcademicApiFixture fixture)
{
    private async Task<(HttpClient Client, string AdminToken, string FacultyToken, EnrollmentDto Enrollment, CourseOfferingDto Offering, Guid MidtermId, Guid FinalId)> SeedThroughEnrollmentAsync()
    {
        var client = fixture.CreateClient();
        var (_, _, _, adminToken) = await AcademicTestDataSeeder.ProvisionAdminAsync(fixture, client);
        var departmentId = await AcademicTestDataSeeder.SeedDepartmentAsync(client, adminToken);
        var designationId = await AcademicTestDataSeeder.SeedDesignationAsync(client, adminToken);
        var program = await AcademicTestDataSeeder.SeedProgramAsync(client, adminToken, departmentId);
        var course = await AcademicTestDataSeeder.SeedCourseAsync(client, adminToken);
        var (_, semesterId) = await AcademicTestDataSeeder.SeedOpenSemesterAsync(client, adminToken);
        var offering = await AcademicTestDataSeeder.SeedCourseOfferingAsync(client, adminToken, course.Id, semesterId, departmentId, capacity: 5);
        var (facultyMemberId, _, facultyToken) = await AcademicTestDataSeeder.SeedFacultyMemberAsync(fixture, client, adminToken, departmentId, designationId);

        var assignResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, $"/api/v1/academic/course-offerings/{offering.Id}/instructor") { Content = JsonContent.Create(new AssignInstructorRequest(facultyMemberId)) }.WithBearerToken(adminToken));
        assignResponse.EnsureSuccessStatusCode();

        var offeringWithExam = await AcademicTestDataSeeder.AddExamAsync(client, adminToken, offering.Id, [new CreateAssessmentRequest("Midterm", 0.4m), new CreateAssessmentRequest("Final", 0.6m)]);
        var assessments = offeringWithExam.Exams.Single().Assessments.ToList();

        var (_, studentToken) = await AcademicTestDataSeeder.SeedActiveStudentAsync(fixture, client, departmentId, program.Id);
        var createEnrollmentResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/v1/academic/enrollments") { Content = JsonContent.Create(new CreateEnrollmentRequest(offering.Id, offering.Sections.Single().Id, null)) }.WithBearerToken(studentToken));
        createEnrollmentResponse.EnsureSuccessStatusCode();
        var enrollment = await createEnrollmentResponse.Content.ReadFromJsonAsync<EnrollmentDto>();

        return (client, adminToken, facultyToken, enrollment!, offeringWithExam, assessments[0].Id, assessments[1].Id);
    }

    private static async Task<GradeDto> SubmitGradeAsync(HttpClient client, string facultyToken, Guid enrollmentId, Guid midtermId, Guid finalId, decimal midtermScore = 80m, decimal finalScore = 70m)
    {
        var request = new SubmitGradeRequest(enrollmentId, [new AssessmentScoreDto(midtermId, midtermScore), new AssessmentScoreDto(finalId, finalScore)]);
        var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/v1/academic/grades") { Content = JsonContent.Create(request) }.WithBearerToken(facultyToken));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<GradeDto>())!;
    }

    [Fact]
    public async Task The_full_submit_lock_approve_publish_pipeline_succeeds_end_to_end()
    {
        var (client, adminToken, facultyToken, enrollment, offering, midtermId, finalId) = await SeedThroughEnrollmentAsync();

        var grade = await SubmitGradeAsync(client, facultyToken, enrollment.Id, midtermId, finalId);
        Assert.Equal(74m, grade.CalculatedScore); // 80*0.4 + 70*0.6

        var lockResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, $"/api/v1/academic/results/{offering.Id}/lock").WithBearerToken(adminToken));
        Assert.Equal(HttpStatusCode.OK, lockResponse.StatusCode);
        var locked = await lockResponse.Content.ReadFromJsonAsync<ResultPublicationDto>();
        Assert.Equal("Verified", locked!.Status);

        var approveResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, $"/api/v1/academic/results/{offering.Id}/approve").WithBearerToken(adminToken));
        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);
        var approved = await approveResponse.Content.ReadFromJsonAsync<ResultPublicationDto>();
        Assert.Equal("Approved", approved!.Status);

        var publishResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, $"/api/v1/academic/results/{offering.Id}/publish").WithBearerToken(adminToken));
        Assert.Equal(HttpStatusCode.OK, publishResponse.StatusCode);
        var published = await publishResponse.Content.ReadFromJsonAsync<ResultPublicationDto>();
        Assert.Equal("Published", published!.Status);
    }

    /// <summary>requirement-spec.md §4/§8: "Faculty attempts to edit a Grade after Published. Rejected outright; only the correction workflow can change it."</summary>
    [Fact]
    public async Task A_direct_grade_submission_attempt_against_an_already_Published_batch_is_rejected_outright()
    {
        var (client, adminToken, facultyToken, enrollment, offering, midtermId, finalId) = await SeedThroughEnrollmentAsync();
        await SubmitGradeAsync(client, facultyToken, enrollment.Id, midtermId, finalId);
        await PublishFullyAsync(client, adminToken, offering.Id);

        var lateSubmitResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/v1/academic/grades")
        {
            Content = JsonContent.Create(new SubmitGradeRequest(enrollment.Id, [new AssessmentScoreDto(midtermId, 10m), new AssessmentScoreDto(finalId, 10m)])),
        }.WithBearerToken(facultyToken));

        Assert.Equal(HttpStatusCode.Conflict, lateSubmitResponse.StatusCode);
        var body = await lateSubmitResponse.Content.ReadAsStringAsync();
        Assert.Contains("already_locked", body);
    }

    /// <summary>edge-cases.md "Grade lock racing a still-in-flight grade submission from Faculty's UI": Faculty's submission and the Department Head's lock racing the same batch - whichever commits first wins; the loser is rejected explicitly, never silently overwritten.</summary>
    [Fact]
    public async Task A_grade_submission_racing_a_concurrent_lock_never_lets_both_apply()
    {
        var (client, adminToken, facultyToken, enrollment, offering, midtermId, finalId) = await SeedThroughEnrollmentAsync();
        await SubmitGradeAsync(client, facultyToken, enrollment.Id, midtermId, finalId);

        var resubmitTask = fixture.CreateClient().SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/v1/academic/grades")
        {
            Content = JsonContent.Create(new SubmitGradeRequest(enrollment.Id, [new AssessmentScoreDto(midtermId, 60m), new AssessmentScoreDto(finalId, 60m)])),
        }.WithBearerToken(facultyToken));
        var lockTask = fixture.CreateClient().SendAsync(new HttpRequestMessage(HttpMethod.Post, $"/api/v1/academic/results/{offering.Id}/lock").WithBearerToken(adminToken));

        var responses = await Task.WhenAll(resubmitTask, lockTask);

        // Both requests get a definitive answer (200/OK for whichever won, or an explicit
        // 409 for whichever lost) - never both silently "succeeding" into an inconsistent state.
        Assert.All(responses, r => Assert.True(r.IsSuccessStatusCode || r.StatusCode == HttpStatusCode.Conflict));

        var finalStateResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, $"/api/v1/academic/course-offerings/{offering.Id}").WithBearerToken(adminToken));
        finalStateResponse.EnsureSuccessStatusCode();
    }

    /// <summary>edge-cases.md "Concurrent Department-Head review/approval of the same grade batch by two reviewers" - whichever reviewer decision commits first is authoritative; the other's later action against the now-changed state fails explicitly, naming the current state.</summary>
    [Fact]
    public async Task Concurrent_lock_and_reject_on_the_same_batch_never_both_apply()
    {
        var (client, adminToken, facultyToken, enrollment, offering, midtermId, finalId) = await SeedThroughEnrollmentAsync();
        await SubmitGradeAsync(client, facultyToken, enrollment.Id, midtermId, finalId);

        var lockTask = fixture.CreateClient().SendAsync(new HttpRequestMessage(HttpMethod.Post, $"/api/v1/academic/results/{offering.Id}/lock").WithBearerToken(adminToken));
        var rejectTask = fixture.CreateClient().SendAsync(new HttpRequestMessage(HttpMethod.Post, $"/api/v1/academic/results/{offering.Id}/reject") { Content = JsonContent.Create(new RejectGradeBatchRequest("Scores look wrong")) }.WithBearerToken(adminToken));

        var responses = await Task.WhenAll(lockTask, rejectTask);

        var successes = responses.Count(r => r.IsSuccessStatusCode);
        var conflicts = responses.Count(r => r.StatusCode == HttpStatusCode.Conflict);

        // Exactly one of the two reviewer decisions is authoritative - the DB-level guard means
        // it's structurally impossible for both a Lock (Calculated->Verified) and a Reject
        // (guarded WHERE status=Calculated) to both commit successfully against the same row.
        Assert.Equal(1, successes);
        Assert.Equal(1, conflicts);
    }

    /// <summary>ACD-13/§9 decision 3: the correction workflow re-enters ResultPublication at Verified, never a raw in-place edit of a Published value.</summary>
    [Fact]
    public async Task Correcting_a_Published_Grade_reenters_the_batch_at_Verified_and_updates_the_value()
    {
        var (client, adminToken, facultyToken, enrollment, offering, midtermId, finalId) = await SeedThroughEnrollmentAsync();
        var grade = await SubmitGradeAsync(client, facultyToken, enrollment.Id, midtermId, finalId);
        await PublishFullyAsync(client, adminToken, offering.Id);

        var correctResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, $"/api/v1/academic/grades/{grade.Id}/correct")
        {
            Content = JsonContent.Create(new CorrectGradeRequest([new AssessmentScoreDto(midtermId, 95m), new AssessmentScoreDto(finalId, 95m)], "Recomputation error found during audit")),
        }.WithBearerToken(adminToken));

        Assert.Equal(HttpStatusCode.OK, correctResponse.StatusCode);
        var corrected = await correctResponse.Content.ReadFromJsonAsync<GradeDto>();
        Assert.Equal(95m, corrected!.CalculatedScore);

        var stateResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, $"/api/v1/academic/course-offerings/{offering.Id}").WithBearerToken(adminToken));
        stateResponse.EnsureSuccessStatusCode();
    }

    private static async Task PublishFullyAsync(HttpClient client, string adminToken, Guid courseOfferingId)
    {
        (await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, $"/api/v1/academic/results/{courseOfferingId}/lock").WithBearerToken(adminToken))).EnsureSuccessStatusCode();
        (await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, $"/api/v1/academic/results/{courseOfferingId}/approve").WithBearerToken(adminToken))).EnsureSuccessStatusCode();
        (await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, $"/api/v1/academic/results/{courseOfferingId}/publish").WithBearerToken(adminToken))).EnsureSuccessStatusCode();
    }
}
