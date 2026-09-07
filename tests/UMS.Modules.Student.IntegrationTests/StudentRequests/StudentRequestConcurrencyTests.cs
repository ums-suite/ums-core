using System.Net;
using System.Net.Http.Json;
using UMS.Modules.Student.Application.StudentRequests;
using UMS.Modules.Student.IntegrationTests.Infrastructure;

namespace UMS.Modules.Student.IntegrationTests.StudentRequests;

/// <summary>
/// STU-9/STU-10/STU-11's own concurrency-sensitive invariant (ums-conventions.md, Testing:
/// "concurrency-sensitive invariants require an explicit concurrent-access test") - edge-cases.md's
/// "Two concurrent StudentRequest submissions of the same type," resolved by design-decisions.md's
/// "StudentRequest Dedup Mechanism" (a partial unique DB index, never an application-level-only
/// check). Fires genuinely concurrent HTTP calls, exactly like <c>CreateStudentRecordConcurrencyTests</c>.
/// </summary>
[Collection(StudentApiTestCollectionDefinition.Name)]
public sealed class StudentRequestConcurrencyTests(StudentApiFixture fixture)
{
    [Fact]
    public async Task Concurrent_submissions_of_the_same_request_type_for_the_same_student_produce_exactly_one_open_request()
    {
        var client = fixture.CreateClient();
        var (_, _, _, adminToken) = await StudentTestDataSeeder.ProvisionAdminAsync(fixture, client);
        var departmentId = await StudentTestDataSeeder.SeedDepartmentAsync(client, adminToken);
        var created = await StudentTestDataSeeder.CreateStudentRecordAsync(fixture, departmentId);
        var login = await StudentTestDataSeeder.ResetPasswordAndLoginAsync(fixture, client, created.IdentityUserId!.Value, "a-Kn0wn-Passw0rd!");

        var tasks = Enumerable.Range(0, 8).Select(i => client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/v1/student/students/requests")
        {
            Content = JsonContent.Create(new SubmitStudentRequestRequest("TranscriptRequest", null, $"attempt {i}", null, false)),
        }.WithBearerToken(login.AccessToken)));

        var responses = await Task.WhenAll(tasks);

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        Assert.Equal(7, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));

        // The DB-level partial unique index is what actually enforced this - no created row was
        // silently dropped, and exactly one StudentRequest of this type exists in the open state.
        var winner = responses.Single(r => r.StatusCode == HttpStatusCode.Created);
        var winnerDto = await winner.Content.ReadFromJsonAsync<StudentRequestDto>();
        Assert.Equal("Submitted", winnerDto!.Status);
    }
}
