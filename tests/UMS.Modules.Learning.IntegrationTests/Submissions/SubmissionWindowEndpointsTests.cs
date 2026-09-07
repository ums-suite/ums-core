using System.Net;
using UMS.Modules.Learning.Application.Assignments;
using UMS.Modules.Learning.Application.Submissions;
using UMS.Modules.Learning.IntegrationTests.Assignments;
using UMS.Modules.Learning.IntegrationTests.Infrastructure;

namespace UMS.Modules.Learning.IntegrationTests.Submissions;

/// <summary>
/// LRN-6: the submission-window accept/reject boundary over the real HTTP surface - on time, in
/// grace, in a penalty tier, and past hard close - plus the extension that widens acceptance for
/// exactly one Student.
///
/// <para>
/// Every case here is driven by moving the WINDOW relative to the real server clock rather than by
/// faking the clock, because requirement-spec.md §4's invariant is precisely that
/// <c>submittedAt</c> comes from the server and nothing a client sends can influence it - a test
/// that injected a timestamp would be testing something this module deliberately does not support.
/// </para>
/// </summary>
[Collection(LearningApiTestCollectionDefinition.Name)]
public sealed class SubmissionWindowEndpointsTests(LearningApiFixture fixture)
{
    private static readonly IReadOnlyCollection<LatePenaltyTierDto> Tiers =
    [
        new(TimeSpan.FromHours(24), 10m),
        new(TimeSpan.FromHours(72), 25m),
    ];

    [Fact]
    public async Task A_submission_well_before_the_deadline_is_accepted_on_time_with_no_penalty()
    {
        var (client, context) = await SeedAsync();
        var assignment = await AssignmentEndpointsTests.PublishedAssignmentAsync(
            client,
            context,
            AssignmentEndpointsTests.NewAssignment(context.CourseOfferingId, deadlineFromNow: TimeSpan.FromDays(7), tiers: Tiers));

        var submission = await SubmitAsync(client, assignment.Id, context.StudentToken, "On time.");

        Assert.False(submission.IsLate);
        Assert.Equal(0m, submission.LatePenaltyPercentage);
        Assert.Equal("Submitted", submission.Status);
    }

    /// <summary>edge-cases.md's headline scenario: an upload that finishes just past the stated deadline lands inside the disclosed grace period and is still on time, penalty-free.</summary>
    [Fact]
    public async Task A_submission_inside_the_grace_period_is_still_on_time_with_no_penalty()
    {
        var (client, context) = await SeedAsync();
        var assignment = await AssignmentEndpointsTests.PublishedAssignmentAsync(
            client,
            context,
            AssignmentEndpointsTests.NewAssignment(
                context.CourseOfferingId,
                deadlineFromNow: TimeSpan.FromSeconds(-30),
                gracePeriod: TimeSpan.FromMinutes(30),
                hardCloseFromNow: TimeSpan.FromDays(5),
                tiers: Tiers));

        var submission = await SubmitAsync(client, assignment.Id, context.StudentToken, "Two seconds late, inside grace.");

        Assert.False(submission.IsLate);
        Assert.Equal(0m, submission.LatePenaltyPercentage);
    }

    [Fact]
    public async Task A_submission_past_the_grace_period_lands_in_the_first_penalty_tier()
    {
        var (client, context) = await SeedAsync();
        var assignment = await AssignmentEndpointsTests.PublishedAssignmentAsync(
            client,
            context,
            AssignmentEndpointsTests.NewAssignment(
                context.CourseOfferingId,
                deadlineFromNow: TimeSpan.FromHours(-2),
                gracePeriod: TimeSpan.FromMinutes(5),
                hardCloseFromNow: TimeSpan.FromDays(5),
                tiers: Tiers));

        var submission = await SubmitAsync(client, assignment.Id, context.StudentToken, "Two hours late.");

        Assert.True(submission.IsLate);
        Assert.Equal(10m, submission.LatePenaltyPercentage);
    }

    [Fact]
    public async Task A_submission_past_the_first_tier_lands_in_the_second()
    {
        var (client, context) = await SeedAsync();
        var assignment = await AssignmentEndpointsTests.PublishedAssignmentAsync(
            client,
            context,
            AssignmentEndpointsTests.NewAssignment(
                context.CourseOfferingId,
                deadlineFromNow: TimeSpan.FromHours(-30),
                gracePeriod: TimeSpan.FromMinutes(5),
                hardCloseFromNow: TimeSpan.FromDays(5),
                tiers: Tiers));

        var submission = await SubmitAsync(client, assignment.Id, context.StudentToken, "Thirty hours late.");

        Assert.True(submission.IsLate);
        Assert.Equal(25m, submission.LatePenaltyPercentage);
    }

    [Fact]
    public async Task A_submission_past_hardCloseAt_is_rejected_outright()
    {
        var (client, context) = await SeedAsync();
        var assignment = await AssignmentEndpointsTests.PublishedAssignmentAsync(
            client,
            context,
            AssignmentEndpointsTests.NewAssignment(
                context.CourseOfferingId,
                deadlineFromNow: TimeSpan.FromHours(-48),
                gracePeriod: TimeSpan.Zero,
                hardCloseFromNow: TimeSpan.FromHours(-1),
                tiers: Tiers));

        var response = await client.PostAsync(
            $"/api/v1/learning/assignments/{assignment.Id}/submissions",
            context.StudentToken,
            new CreateSubmissionRequest("Way too late.", null));

        await response.AssertProblemCodeAsync(HttpStatusCode.Conflict, "submission.window_closed");
    }

    [Fact]
    public async Task A_submission_to_an_unpublished_Assignment_is_rejected()
    {
        var (client, context) = await SeedAsync();
        var draft = await client.PostAndReadAsync<AssignmentDto>(
            "/api/v1/learning/assignments",
            context.InstructorToken,
            AssignmentEndpointsTests.NewAssignment(context.CourseOfferingId));

        var response = await client.PostAsync(
            $"/api/v1/learning/assignments/{draft.Id}/submissions",
            context.StudentToken,
            new CreateSubmissionRequest("Too early.", null));

        await response.AssertProblemCodeAsync(HttpStatusCode.Conflict, "submission.assignment_not_published");
    }

    [Fact]
    public async Task A_Student_who_is_not_enrolled_cannot_submit()
    {
        var (client, context) = await SeedAsync();
        var assignment = await AssignmentEndpointsTests.PublishedAssignmentAsync(client, context);
        var outsiderToken = await (await ScenarioAsync()).UnenrolledStudentTokenAsync();

        var response = await client.PostAsync(
            $"/api/v1/learning/assignments/{assignment.Id}/submissions",
            outsiderToken,
            new CreateSubmissionRequest("Not my course.", null));

        await response.AssertProblemCodeAsync(HttpStatusCode.Forbidden, "submission.not_enrolled");
    }

    /// <summary>
    /// LRN-3's whole point: the extension widens acceptance for EXACTLY the named Student and leaves
    /// every other Student's own effective deadline completely unaffected
    /// (requirement-spec.md §4).
    /// </summary>
    [Fact]
    public async Task An_extension_widens_acceptance_for_exactly_one_Student()
    {
        var (client, context) = await SeedAsync();
        var assignment = await AssignmentEndpointsTests.PublishedAssignmentAsync(
            client,
            context,
            AssignmentEndpointsTests.NewAssignment(
                context.CourseOfferingId,
                deadlineFromNow: TimeSpan.FromHours(-48),
                gracePeriod: TimeSpan.Zero,
                hardCloseFromNow: TimeSpan.FromHours(-1),
                tiers: Tiers));

        var extension = await client.PostAndReadAsync<SubmissionExtensionDto>(
            $"/api/v1/learning/assignments/{assignment.Id}/extensions",
            context.InstructorToken,
            new GrantSubmissionExtensionRequest(context.StudentId, DateTimeOffset.UtcNow.AddDays(3), "Hospitalized, documented.", WaivesLatePenalty: false));

        Assert.Equal(context.StudentId, extension.StudentId);

        var extended = await SubmitAsync(client, assignment.Id, context.StudentToken, "Accommodated.");
        Assert.True(extended.IsLate);
        Assert.Equal(25m, extended.LatePenaltyPercentage);

        var otherStudent = await client.PostAsync(
            $"/api/v1/learning/assignments/{assignment.Id}/submissions",
            context.SecondStudentToken,
            new CreateSubmissionRequest("No extension for me.", null));
        await otherStudent.AssertProblemCodeAsync(HttpStatusCode.Conflict, "submission.window_closed");
    }

    [Fact]
    public async Task An_extension_that_waives_the_late_penalty_produces_a_zero_deduction()
    {
        var (client, context) = await SeedAsync();
        var assignment = await AssignmentEndpointsTests.PublishedAssignmentAsync(
            client,
            context,
            AssignmentEndpointsTests.NewAssignment(
                context.CourseOfferingId,
                deadlineFromNow: TimeSpan.FromHours(-48),
                gracePeriod: TimeSpan.Zero,
                hardCloseFromNow: TimeSpan.FromHours(-1),
                tiers: Tiers));

        await client.PostAndReadAsync<SubmissionExtensionDto>(
            $"/api/v1/learning/assignments/{assignment.Id}/extensions",
            context.InstructorToken,
            new GrantSubmissionExtensionRequest(context.StudentId, DateTimeOffset.UtcNow.AddDays(3), "University-side outage.", WaivesLatePenalty: true));

        var submission = await SubmitAsync(client, assignment.Id, context.StudentToken, "Waived.");

        Assert.True(submission.IsLate);
        Assert.Equal(0m, submission.LatePenaltyPercentage);
    }

    [Fact]
    public async Task An_extension_for_a_Student_not_enrolled_in_the_offering_is_rejected()
    {
        var (client, context) = await SeedAsync();
        var assignment = await AssignmentEndpointsTests.PublishedAssignmentAsync(client, context);

        var response = await client.PostAsync(
            $"/api/v1/learning/assignments/{assignment.Id}/extensions",
            context.InstructorToken,
            new GrantSubmissionExtensionRequest(Guid.NewGuid(), DateTimeOffset.UtcNow.AddDays(3), "Unknown student.", WaivesLatePenalty: false));

        await response.AssertProblemCodeAsync(HttpStatusCode.BadRequest, "assignment.student_not_enrolled");
    }

    /// <summary>requirement-spec.md §5 Auditability: a SubmissionExtension grant is a sensitive mutation written synchronously to Audit, in the same transaction as the grant itself.</summary>
    [Fact]
    public async Task Granting_an_extension_writes_a_synchronous_Audit_entry()
    {
        var (client, context) = await SeedAsync();
        var assignment = await AssignmentEndpointsTests.PublishedAssignmentAsync(client, context);

        var extension = await client.PostAndReadAsync<SubmissionExtensionDto>(
            $"/api/v1/learning/assignments/{assignment.Id}/extensions",
            context.InstructorToken,
            new GrantSubmissionExtensionRequest(context.StudentId, DateTimeOffset.UtcNow.AddDays(3), "Family emergency.", WaivesLatePenalty: false));

        var count = await AuditEntryCounter.CountAsync(fixture, "SubmissionExtension", extension.Id.ToString());
        Assert.Equal(1, count);
    }

    internal static async Task<SubmissionDto> SubmitAsync(HttpClient client, Guid assignmentId, string studentToken, string text) =>
        await client.PostAndReadAsync<SubmissionDto>(
            $"/api/v1/learning/assignments/{assignmentId}/submissions",
            studentToken,
            new CreateSubmissionRequest(text, null));

    /// <summary>One course context per test class, memoized - see <see cref="LearningScenario"/> for why this is not seeded per test.</summary>
    private async Task<(HttpClient Client, LearningTestDataSeeder.CourseContext Context)> SeedAsync()
    {
        var scenario = await LearningScenario.GetAsync(fixture, "submission-window");
        return (scenario.Client, scenario.Context);
    }

    private Task<LearningScenario> ScenarioAsync() => LearningScenario.GetAsync(fixture, "submission-window");
}
