using System.Net.Http.Json;
using UMS.Modules.Academic.Application.CourseOfferings;
using UMS.Modules.Academic.Application.Enrollments;
using UMS.Modules.Academic.Application.Grades;
using UMS.Modules.Academic.Application.ResultPublications;
using UMS.Modules.Academic.IntegrationTests.Infrastructure;

namespace UMS.Modules.Academic.IntegrationTests.Transcripts;

/// <summary>
/// ACD-14/ACD-15: requirement-spec.md §4's Transcript-sourcing invariant and edge-cases.md
/// "Transcript requested mid-semester" - the in-progress semester's grades are excluded entirely,
/// not shown as partial/provisional, verified by querying the SAME endpoint before and after
/// publish rather than asserting against mocked state.
/// </summary>
[Collection(AcademicApiTestCollectionDefinition.Name)]
public sealed class TranscriptTests(AcademicApiFixture fixture)
{
    [Fact]
    public async Task A_grade_batch_not_yet_Published_is_excluded_entirely_from_the_Transcript_then_appears_once_Published()
    {
        var client = fixture.CreateClient();
        var (_, _, _, adminToken) = await AcademicTestDataSeeder.ProvisionAdminAsync(fixture, client);
        var departmentId = await AcademicTestDataSeeder.SeedDepartmentAsync(client, adminToken);
        var designationId = await AcademicTestDataSeeder.SeedDesignationAsync(client, adminToken);
        var program = await AcademicTestDataSeeder.SeedProgramAsync(client, adminToken, departmentId);
        var course = await AcademicTestDataSeeder.SeedCourseAsync(client, adminToken);
        var (_, semesterId) = await AcademicTestDataSeeder.SeedOpenSemesterAsync(client, adminToken);
        var offering = await AcademicTestDataSeeder.SeedCourseOfferingAsync(client, adminToken, course.Id, semesterId, departmentId, capacity: 5);
        var (facultyMemberId, _, facultyToken) = await AcademicTestDataSeeder.SeedFacultyMemberAsync(client, adminToken, departmentId, designationId);
        (await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, $"/api/v1/academic/course-offerings/{offering.Id}/instructor") { Content = JsonContent.Create(new AssignInstructorRequest(facultyMemberId)) }.WithBearerToken(adminToken))).EnsureSuccessStatusCode();
        var offeringWithExam = await AcademicTestDataSeeder.AddExamAsync(client, adminToken, offering.Id, [new CreateAssessmentRequest("Only", 1.0m)]);
        var assessmentId = offeringWithExam.Exams.Single().Assessments.Single().Id;

        var (studentId, studentToken) = await AcademicTestDataSeeder.SeedActiveStudentAsync(fixture, client, departmentId, program.Id);
        var enrollResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/v1/academic/enrollments") { Content = JsonContent.Create(new CreateEnrollmentRequest(offering.Id, offering.Sections.Single().Id, null)) }.WithBearerToken(studentToken));
        enrollResponse.EnsureSuccessStatusCode();
        var enrollment = await enrollResponse.Content.ReadFromJsonAsync<EnrollmentDto>();

        var gradeResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/v1/academic/grades") { Content = JsonContent.Create(new SubmitGradeRequest(enrollment!.Id, [new AssessmentScoreDto(assessmentId, 88m)])) }.WithBearerToken(facultyToken));
        gradeResponse.EnsureSuccessStatusCode();

        // Mid-semester: the batch is Calculated, not Published - the Transcript must show nothing.
        var midSemesterTranscript = await GetTranscriptAsync(client, studentToken, studentId);
        Assert.Empty(midSemesterTranscript.Results);
        Assert.Null(midSemesterTranscript.OverallAverageScore);

        var midSemesterResults = await GetResultsAsync(client, studentToken, studentId);
        Assert.Empty(midSemesterResults);

        // Publish the full pipeline.
        (await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, $"/api/v1/academic/results/{offering.Id}/lock").WithBearerToken(adminToken))).EnsureSuccessStatusCode();
        (await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, $"/api/v1/academic/results/{offering.Id}/approve").WithBearerToken(adminToken))).EnsureSuccessStatusCode();
        (await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, $"/api/v1/academic/results/{offering.Id}/publish").WithBearerToken(adminToken))).EnsureSuccessStatusCode();

        // After publish: the Transcript now includes exactly this one result.
        var afterPublishTranscript = await GetTranscriptAsync(client, studentToken, studentId);
        var row = Assert.Single(afterPublishTranscript.Results);
        Assert.Equal(88m, row.CalculatedScore);
        Assert.Equal(88m, afterPublishTranscript.OverallAverageScore);

        var afterPublishResults = await GetResultsAsync(client, studentToken, studentId);
        Assert.Single(afterPublishResults);
    }

    private static async Task<TranscriptDto> GetTranscriptAsync(HttpClient client, string token, Guid studentId)
    {
        var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, $"/api/v1/academic/students/{studentId}/transcript").WithBearerToken(token));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TranscriptDto>())!;
    }

    private static async Task<IReadOnlyList<StudentResultRowDto>> GetResultsAsync(HttpClient client, string token, Guid studentId)
    {
        var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, $"/api/v1/academic/students/{studentId}/results").WithBearerToken(token));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IReadOnlyList<StudentResultRowDto>>())!;
    }
}
