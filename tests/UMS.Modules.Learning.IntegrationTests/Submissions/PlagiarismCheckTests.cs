using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Learning.Application.Assignments;
using UMS.Modules.Learning.Application.PlagiarismChecks;
using UMS.Modules.Learning.Application.Submissions;
using UMS.Modules.Learning.IntegrationTests.Assignments;
using UMS.Modules.Learning.IntegrationTests.Infrastructure;

namespace UMS.Modules.Learning.IntegrationTests.Submissions;

/// <summary>
/// LRN-8/LRN-9 driven against the real, Polly-wrapped provider pipeline: triggering on
/// <c>SubmissionCreated</c>, cancellation on supersession, the <c>Failed</c> terminal status under a
/// simulated outage, and that a <c>Failed</c> check never blocks evaluation.
///
/// <para>
/// The dispatch/window-close relays themselves live in <c>UMS.Workers</c>, which this Host-backed
/// fixture does not run, so these tests call the same <see cref="PlagiarismCheckService"/> methods
/// those workers call - the identical shape Faculty's own suite uses for its outbox-driven
/// projection. The provider call, its resilience pipeline, and every state transition are real.
/// </para>
/// </summary>
[Collection(LearningApiTestCollectionDefinition.Name)]
public sealed class PlagiarismCheckTests(LearningApiFixture fixture)
{
    private const string ForceOutageKey = "Learning__PlagiarismProvider__ForceOutage";

    [Fact]
    public async Task A_check_triggered_on_SubmissionCreated_runs_to_Completed_and_carries_a_real_score()
    {
        var (client, context) = await SeedAsync();
        var submission = await SubmittedAsync(client, context);

        await EnqueueAndRunAsync(submission.Id);

        var check = await client.GetAndReadAsync<PlagiarismCheckDto>(
            $"/api/v1/learning/submissions/{submission.Id}/plagiarism-check",
            context.InstructorToken);

        Assert.Equal("Completed", check.Status);
        Assert.NotNull(check.SimilarityPercentage);
        Assert.Equal("ums-fake-similarity-gateway", check.ProviderName);
        Assert.Equal(1, check.AttemptCount);
        Assert.Null(check.FailureReason);
        Assert.Equal(1, await AuditEntryCounter.CountOutboxAsync(fixture, "PlagiarismCheckCompleted", submission.Id.ToString()));
    }

    /// <summary>design-decisions.md: a check whose Submission is superseded before it completes is cancelled/discarded, so metered provider quota is never spent on content a resubmission already made moot.</summary>
    [Fact]
    public async Task A_check_is_cancelled_when_its_Submission_is_superseded_before_it_completes()
    {
        var (client, context) = await SeedAsync();
        var assignment = await AssignmentEndpointsTests.PublishedAssignmentAsync(client, context);
        var first = await SubmissionWindowEndpointsTests.SubmitAsync(client, assignment.Id, context.StudentToken, "First attempt.");

        // The check is enqueued (as the relay would on SubmissionCreated) but not yet run...
        using (var scope = fixture.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<PlagiarismCheckService>();
            var enqueued = await service.EnqueueForSubmissionAsync(first.Id);
            Assert.True(enqueued.IsSuccess, enqueued.Error?.Message);
        }

        // ...and then the Student resubmits before the worker's next pass.
        await SubmissionWindowEndpointsTests.SubmitAsync(client, assignment.Id, context.StudentToken, "Second attempt.");

        var superseded = await client.GetAndReadAsync<SubmissionDto>($"/api/v1/learning/submissions/{first.Id}", context.StudentToken);

        Assert.Equal("Superseded", superseded.Status);
        Assert.Equal("Cancelled", superseded.PlagiarismCheck!.Status);
    }

    /// <summary>
    /// edge-cases.md "The plagiarism-check provider is slow or down": the Polly pipeline's retries
    /// are exhausted against a forced outage and the check becomes a real, terminal <c>Failed</c>
    /// with a reason - NEVER a fabricated clean result, and never an indefinite "still checking".
    /// </summary>
    [Fact]
    public async Task A_provider_outage_produces_a_terminal_Failed_status_with_a_reason_never_a_clean_result()
    {
        var (client, context) = await SeedAsync();
        var submission = await SubmittedAsync(client, context);

        await WithForcedProviderOutageAsync(async () => await EnqueueAndRunAsync(submission.Id));

        var check = await client.GetAndReadAsync<PlagiarismCheckDto>(
            $"/api/v1/learning/submissions/{submission.Id}/plagiarism-check",
            context.InstructorToken);

        Assert.Equal("Failed", check.Status);
        Assert.Null(check.SimilarityPercentage);
        Assert.False(string.IsNullOrWhiteSpace(check.FailureReason));
        Assert.Equal(1, await AuditEntryCounter.CountOutboxAsync(fixture, "PlagiarismCheckFailed", submission.Id.ToString()));
    }

    /// <summary>requirement-spec.md §4's headline invariant, over the real HTTP surface: a Failed check never gates or auto-rejects evaluation.</summary>
    [Fact]
    public async Task A_Failed_check_never_blocks_the_Instructor_from_evaluating_the_Submission()
    {
        var (client, context) = await SeedAsync();
        var submission = await SubmittedAsync(client, context);
        await WithForcedProviderOutageAsync(async () => await EnqueueAndRunAsync(submission.Id));

        var evaluated = await client.PostAndReadAsync<SubmissionDto>(
            $"/api/v1/learning/submissions/{submission.Id}/evaluate",
            context.InstructorToken,
            new EvaluateSubmissionRequest(77m, "Graded without a similarity signal."));

        Assert.Equal(77m, evaluated.Score!.RawPoints);
        Assert.Equal("Failed", evaluated.PlagiarismCheck!.Status);
    }

    [Fact]
    public async Task An_Instructor_can_manually_retry_a_Failed_check_and_it_then_completes()
    {
        var (client, context) = await SeedAsync();
        var submission = await SubmittedAsync(client, context);
        await WithForcedProviderOutageAsync(async () => await EnqueueAndRunAsync(submission.Id));

        var requeued = await client.PostAndReadAsync<PlagiarismCheckDto>(
            $"/api/v1/learning/submissions/{submission.Id}/plagiarism-check/retry",
            context.InstructorToken);
        Assert.Equal("Queued", requeued.Status);

        using (var scope = fixture.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<PlagiarismCheckService>().RunQueuedAsync(10);
        }

        var check = await client.GetAndReadAsync<PlagiarismCheckDto>(
            $"/api/v1/learning/submissions/{submission.Id}/plagiarism-check",
            context.InstructorToken);

        Assert.Equal("Completed", check.Status);
        Assert.Equal(2, check.AttemptCount);
    }

    /// <summary>
    /// LRN-2/LRN-9's automatic half, driven through the same
    /// <see cref="AssignmentWindowCloseService"/> the <c>UMS.Workers</c> sweep calls: an Assignment
    /// whose <c>hardCloseAt</c> has passed is closed (raising <c>AssignmentClosed</c>), and its
    /// counted Submission - whose earlier check Failed during a provider outage - gets the second
    /// attempt edge-cases.md's own residual note promises, without an Instructor having to notice.
    /// </summary>
    [Fact]
    public async Task The_window_close_sweep_closes_an_elapsed_Assignment_and_re_enqueues_its_unchecked_Submission()
    {
        var (client, context) = await SeedAsync();

        // A window that is still open right now, so a Submission can land, but closes a moment later.
        var assignment = await AssignmentEndpointsTests.PublishedAssignmentAsync(
            client,
            context,
            AssignmentEndpointsTests.NewAssignment(
                context.CourseOfferingId,
                deadlineFromNow: TimeSpan.FromSeconds(2),
                gracePeriod: TimeSpan.Zero,
                hardCloseFromNow: TimeSpan.FromSeconds(2)));

        var submission = await SubmissionWindowEndpointsTests.SubmitAsync(client, assignment.Id, context.StudentToken, "Just in time.");
        await WithForcedProviderOutageAsync(async () => await EnqueueAndRunAsync(submission.Id));

        var failed = await client.GetAndReadAsync<PlagiarismCheckDto>(
            $"/api/v1/learning/submissions/{submission.Id}/plagiarism-check",
            context.InstructorToken);
        Assert.Equal("Failed", failed.Status);

        await Task.Delay(TimeSpan.FromSeconds(3));

        int closed;
        using (var scope = fixture.Services.CreateScope())
        {
            closed = await scope.ServiceProvider.GetRequiredService<AssignmentWindowCloseService>().CloseElapsedWindowsAsync(50);
        }

        Assert.True(closed >= 1);

        var closedAssignment = await client.GetAndReadAsync<AssignmentDto>($"/api/v1/learning/assignments/{assignment.Id}", context.InstructorToken);
        Assert.Equal("Closed", closedAssignment.Status);
        Assert.Equal(1, await AuditEntryCounter.CountOutboxAsync(fixture, "AssignmentClosed", assignment.Id.ToString()));

        var requeued = await client.GetAndReadAsync<PlagiarismCheckDto>(
            $"/api/v1/learning/submissions/{submission.Id}/plagiarism-check",
            context.InstructorToken);
        Assert.Equal("Queued", requeued.Status);
    }

    [Fact]
    public async Task Retrying_an_already_completed_check_is_rejected()
    {
        var (client, context) = await SeedAsync();
        var submission = await SubmittedAsync(client, context);
        await EnqueueAndRunAsync(submission.Id);

        var response = await client.PostAsync(
            $"/api/v1/learning/submissions/{submission.Id}/plagiarism-check/retry",
            context.InstructorToken);

        await response.AssertProblemCodeAsync(HttpStatusCode.Conflict, "plagiarism_check.not_retryable");
    }

    [Fact]
    public async Task A_Student_cannot_read_or_retry_the_plagiarism_check_on_their_own_Submission()
    {
        var (client, context) = await SeedAsync();
        var submission = await SubmittedAsync(client, context);
        await EnqueueAndRunAsync(submission.Id);

        var read = await client.GetAsync($"/api/v1/learning/submissions/{submission.Id}/plagiarism-check", context.StudentToken);
        var retry = await client.PostAsync($"/api/v1/learning/submissions/{submission.Id}/plagiarism-check/retry", context.StudentToken);

        Assert.Equal(HttpStatusCode.Forbidden, read.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, retry.StatusCode);
    }

    private static async Task<SubmissionDto> SubmittedAsync(HttpClient client, LearningTestDataSeeder.CourseContext context)
    {
        var assignment = await AssignmentEndpointsTests.PublishedAssignmentAsync(client, context);
        return await SubmissionWindowEndpointsTests.SubmitAsync(client, assignment.Id, context.StudentToken, $"Answer {Guid.NewGuid():N}.");
    }

    private async Task EnqueueAndRunAsync(Guid submissionId)
    {
        using var scope = fixture.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<PlagiarismCheckService>();

        var enqueued = await service.EnqueueForSubmissionAsync(submissionId);
        Assert.True(enqueued.IsSuccess, enqueued.Error?.Message);

        await service.RunQueuedAsync(10);
    }

    /// <summary>
    /// Flips the fake gateway into a total outage for the duration of one action by reloading the
    /// Host's own configuration root - the same mechanism a real operator would use to disable an
    /// integration, exercised against the genuine <c>IOptionsMonitor</c> the provider reads. Safe
    /// because every test class in this suite shares one xUnit collection and therefore never runs
    /// in parallel with another.
    /// </summary>
    private async Task WithForcedProviderOutageAsync(Func<Task> action)
    {
        var configuration = (IConfigurationRoot)fixture.Services.GetRequiredService<IConfiguration>();

        Environment.SetEnvironmentVariable(ForceOutageKey, "true");
        configuration.Reload();
        try
        {
            await action();
        }
        finally
        {
            Environment.SetEnvironmentVariable(ForceOutageKey, "false");
            configuration.Reload();
        }
    }

    /// <summary>One course context per test class, memoized - see <see cref="LearningScenario"/> for why this is not seeded per test.</summary>
    private async Task<(HttpClient Client, LearningTestDataSeeder.CourseContext Context)> SeedAsync()
    {
        var scenario = await LearningScenario.GetAsync(fixture, "plagiarism");
        return (scenario.Client, scenario.Context);
    }

    private Task<LearningScenario> ScenarioAsync() => LearningScenario.GetAsync(fixture, "plagiarism");
}
