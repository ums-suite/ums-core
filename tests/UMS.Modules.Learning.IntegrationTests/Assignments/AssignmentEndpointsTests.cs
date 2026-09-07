using System.Net;
using UMS.Modules.Learning.Application.Assignments;
using UMS.Modules.Learning.IntegrationTests.Infrastructure;

namespace UMS.Modules.Learning.IntegrationTests.Assignments;

/// <summary>LRN-1/LRN-2/LRN-3/LRN-4: the Assignment HTTP surface, including requirement-spec.md §6's Instructor resource-ownership rule (a permission string is necessary but never sufficient).</summary>
[Collection(LearningApiTestCollectionDefinition.Name)]
public sealed class AssignmentEndpointsTests(LearningApiFixture fixture)
{
    [Fact]
    public async Task An_Instructor_can_create_and_publish_an_Assignment_for_their_own_CourseOffering()
    {
        var (client, context) = await SeedAsync();

        var assignment = await client.PostAndReadAsync<AssignmentDto>("/api/v1/learning/assignments", context.InstructorToken, NewAssignment(context.CourseOfferingId));
        Assert.Equal("Draft", assignment.Status);

        var published = await client.PostAndReadAsync<AssignmentDto>($"/api/v1/learning/assignments/{assignment.Id}/publish", context.InstructorToken);
        Assert.Equal("Published", published.Status);
        Assert.NotNull(published.PublishedAt);
    }

    /// <summary>requirement-spec.md §6: "Instructor actions are additionally gated by a resource-ownership check against the CourseOffering's CourseAssignment ... not merely a ScopeGrant."</summary>
    [Fact]
    public async Task Another_Instructor_holding_the_same_permission_cannot_create_an_Assignment_for_someone_elses_CourseOffering()
    {
        var (client, context) = await SeedAsync();
        var otherInstructorToken = await (await ScenarioAsync()).UnrelatedInstructorTokenAsync();

        var response = await client.PostAsync("/api/v1/learning/assignments", otherInstructorToken, NewAssignment(context.CourseOfferingId));

        await response.AssertProblemCodeAsync(HttpStatusCode.Forbidden, "assignment.not_assigned_instructor");
    }

    [Fact]
    public async Task A_Student_cannot_create_an_Assignment_at_all()
    {
        var (client, context) = await SeedAsync();

        var response = await client.PostAsync("/api/v1/learning/assignments", context.StudentToken, NewAssignment(context.CourseOfferingId));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task An_Assignment_against_a_nonexistent_CourseOffering_is_rejected()
    {
        var (client, context) = await SeedAsync();

        var response = await client.PostAsync("/api/v1/learning/assignments", context.InstructorToken, NewAssignment(Guid.NewGuid()));

        await response.AssertProblemCodeAsync(HttpStatusCode.NotFound, "assignment.courseoffering_not_found");
    }

    [Fact]
    public async Task A_window_whose_hardCloseAt_precedes_its_effective_deadline_is_rejected()
    {
        var (client, context) = await SeedAsync();
        var now = DateTimeOffset.UtcNow;
        var body = new CreateAssignmentRequest(
            context.CourseOfferingId,
            "Impossible window",
            null,
            "TextOrFile",
            true,
            100,
            now.AddMinutes(-1),
            now.AddHours(1),
            TimeSpan.FromHours(2),
            now.AddHours(2),
            null);

        var response = await client.PostAsync("/api/v1/learning/assignments", context.InstructorToken, body);

        await response.AssertProblemCodeAsync(HttpStatusCode.BadRequest, "submission_window.hard_close_before_effective_deadline");
    }

    /// <summary>LRN-4: a Draft Assignment must never leak to an enrolled Student - it has not been published to them yet.</summary>
    [Fact]
    public async Task A_Student_sees_only_published_Assignments_while_the_Instructor_sees_drafts_too()
    {
        var (client, context) = await SeedAsync();
        var draft = await client.PostAndReadAsync<AssignmentDto>("/api/v1/learning/assignments", context.InstructorToken, NewAssignment(context.CourseOfferingId, "Draft one"));
        var published = await client.PostAndReadAsync<AssignmentDto>("/api/v1/learning/assignments", context.InstructorToken, NewAssignment(context.CourseOfferingId, "Published one"));
        await client.PostAndReadAsync<AssignmentDto>($"/api/v1/learning/assignments/{published.Id}/publish", context.InstructorToken);

        var studentView = await client.GetAndReadAsync<List<AssignmentDto>>($"/api/v1/learning/assignments?courseOfferingId={context.CourseOfferingId}", context.StudentToken);
        var instructorView = await client.GetAndReadAsync<List<AssignmentDto>>($"/api/v1/learning/assignments?courseOfferingId={context.CourseOfferingId}", context.InstructorToken);

        Assert.DoesNotContain(studentView, a => a.Id == draft.Id);
        Assert.Contains(studentView, a => a.Id == published.Id);
        Assert.Contains(instructorView, a => a.Id == draft.Id);

        var draftDirect = await client.GetAsync($"/api/v1/learning/assignments/{draft.Id}", context.StudentToken);
        await draftDirect.AssertProblemCodeAsync(HttpStatusCode.NotFound, "assignment.not_found");
    }

    [Fact]
    public async Task A_Student_who_is_not_enrolled_cannot_browse_the_offerings_Assignments()
    {
        var (client, context) = await SeedAsync();
        var outsiderToken = await (await ScenarioAsync()).UnenrolledStudentTokenAsync();

        var response = await client.GetAsync($"/api/v1/learning/assignments?courseOfferingId={context.CourseOfferingId}", outsiderToken);

        await response.AssertProblemCodeAsync(HttpStatusCode.Forbidden, "learning.not_enrolled_or_instructor");
    }

    [Fact]
    public async Task Closing_and_cancelling_move_the_Assignment_through_its_lifecycle()
    {
        var (client, context) = await SeedAsync();
        var assignment = await PublishedAssignmentAsync(client, context);

        var closed = await client.PostAndReadAsync<AssignmentDto>($"/api/v1/learning/assignments/{assignment.Id}/close", context.InstructorToken);
        Assert.Equal("Closed", closed.Status);

        var cancelled = await client.PostAndReadAsync<AssignmentDto>(
            $"/api/v1/learning/assignments/{assignment.Id}/cancel",
            context.InstructorToken,
            new CancelAssignmentRequest("CourseOffering withdrawn."));
        Assert.Equal("Cancelled", cancelled.Status);
        Assert.Equal("CourseOffering withdrawn.", cancelled.CancellationReason);
    }

    [Fact]
    public async Task Publishing_an_already_published_Assignment_returns_a_conflict()
    {
        var (client, context) = await SeedAsync();
        var assignment = await PublishedAssignmentAsync(client, context);

        var response = await client.PostAsync($"/api/v1/learning/assignments/{assignment.Id}/publish", context.InstructorToken);

        await response.AssertProblemCodeAsync(HttpStatusCode.Conflict, "assignment.invalid_transition");
    }

    internal static CreateAssignmentRequest NewAssignment(
        Guid courseOfferingId,
        string title = "Essay on distributed systems",
        TimeSpan? deadlineFromNow = null,
        TimeSpan? gracePeriod = null,
        TimeSpan? hardCloseFromNow = null,
        bool allowResubmission = true,
        IReadOnlyCollection<LatePenaltyTierDto>? tiers = null,
        string allowedSubmissionType = "TextOrFile")
    {
        var now = DateTimeOffset.UtcNow;
        var deadline = now.Add(deadlineFromNow ?? TimeSpan.FromDays(7));

        // Several tests deliberately place the deadline in the PAST (that is how the real
        // server clock is driven past a boundary without faking it), so the window's opening
        // instant has to be earlier still - a fixed "5 minutes ago" would make the window itself
        // invalid rather than exercising the boundary under test.
        var opensAt = deadline.AddDays(-30);

        return new CreateAssignmentRequest(
            courseOfferingId,
            title,
            "Write 2000 words.",
            allowedSubmissionType,
            allowResubmission,
            100,
            opensAt,
            deadline,
            gracePeriod ?? TimeSpan.FromMinutes(5),
            now.Add(hardCloseFromNow ?? TimeSpan.FromDays(10)),
            tiers);
    }

    internal static async Task<AssignmentDto> PublishedAssignmentAsync(HttpClient client, LearningTestDataSeeder.CourseContext context, CreateAssignmentRequest? body = null)
    {
        var assignment = await client.PostAndReadAsync<AssignmentDto>("/api/v1/learning/assignments", context.InstructorToken, body ?? NewAssignment(context.CourseOfferingId));
        return await client.PostAndReadAsync<AssignmentDto>($"/api/v1/learning/assignments/{assignment.Id}/publish", context.InstructorToken);
    }

    /// <summary>One course context per test class, memoized - see <see cref="LearningScenario"/> for why this is not seeded per test.</summary>
    private async Task<(HttpClient Client, LearningTestDataSeeder.CourseContext Context)> SeedAsync()
    {
        var scenario = await LearningScenario.GetAsync(fixture, "assignments");
        return (scenario.Client, scenario.Context);
    }

    private Task<LearningScenario> ScenarioAsync() => LearningScenario.GetAsync(fixture, "assignments");
}
