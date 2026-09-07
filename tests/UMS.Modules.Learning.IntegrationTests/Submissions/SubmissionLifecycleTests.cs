using System.Net;
using UMS.Modules.Learning.Application.Assignments;
using UMS.Modules.Learning.Application.Submissions;
using UMS.Modules.Learning.IntegrationTests.Assignments;
using UMS.Modules.Learning.IntegrationTests.Infrastructure;

namespace UMS.Modules.Learning.IntegrationTests.Submissions;

/// <summary>LRN-5/LRN-7/LRN-10/LRN-11: the presigned-upload flow, the superseded chain, the Instructor's grading queue, and evaluation's fan-out event.</summary>
[Collection(LearningApiTestCollectionDefinition.Name)]
public sealed class SubmissionLifecycleTests(LearningApiFixture fixture)
{
    /// <summary>
    /// LRN-5 end to end against Documents' REAL <c>UploadedArtifact</c> pipeline and real object
    /// storage - request a presigned slot, PUT the bytes straight to MinIO, confirm, then reference
    /// the resulting <c>artifactId</c> from the Submission. Nothing here touches a second storage
    /// mechanism (design-decisions.md, "Raw File Storage via Documents' Object-Storage
    /// Integration").
    /// </summary>
    [Fact]
    public async Task A_Student_uploads_a_file_through_Documents_presigned_flow_and_references_it_from_their_Submission()
    {
        var (client, context) = await SeedAsync();
        var assignment = await AssignmentEndpointsTests.PublishedAssignmentAsync(client, context);

        var slot = await client.PostAndReadAsync<SubmissionUploadSlotDto>(
            $"/api/v1/learning/assignments/{assignment.Id}/submissions/uploads",
            context.StudentToken,
            new RequestSubmissionUploadRequest("essay.txt", "text/plain"));

        Assert.NotNull(slot.UploadUrl);
        Assert.Equal("PendingUpload", slot.Status);

        using (var uploader = new HttpClient())
        {
            using var content = new StringContent("This is the essay body.");
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
            var upload = await uploader.PutAsync(new Uri(slot.UploadUrl!), content);
            upload.EnsureSuccessStatusCode();
        }

        var confirmed = await client.PostAndReadAsync<SubmissionUploadSlotDto>(
            $"/api/v1/learning/assignments/{assignment.Id}/submissions/uploads/confirm",
            context.StudentToken,
            new ConfirmSubmissionUploadRequest(slot.ArtifactId));
        Assert.Equal("Ready", confirmed.Status);

        var submission = await client.PostAndReadAsync<SubmissionDto>(
            $"/api/v1/learning/assignments/{assignment.Id}/submissions",
            context.StudentToken,
            new CreateSubmissionRequest(null, [new SubmissionFileDto(slot.ArtifactId, "essay.txt", "text/plain")]));

        var file = Assert.Single(submission.Files);
        Assert.Equal(slot.ArtifactId, file.ArtifactId);
    }

    [Fact]
    public async Task Confirming_an_upload_whose_bytes_never_landed_is_rejected_rather_than_treated_as_Ready()
    {
        var (client, context) = await SeedAsync();
        var assignment = await AssignmentEndpointsTests.PublishedAssignmentAsync(client, context);

        var slot = await client.PostAndReadAsync<SubmissionUploadSlotDto>(
            $"/api/v1/learning/assignments/{assignment.Id}/submissions/uploads",
            context.StudentToken,
            new RequestSubmissionUploadRequest("never-uploaded.txt", "text/plain"));

        var response = await client.PostAsync(
            $"/api/v1/learning/assignments/{assignment.Id}/submissions/uploads/confirm",
            context.StudentToken,
            new ConfirmSubmissionUploadRequest(slot.ArtifactId));

        await response.AssertProblemCodeAsync(HttpStatusCode.Conflict, "submission_upload.not_ready");
    }

    /// <summary>design-decisions.md "Submission Immutability &amp; Resubmission via Superseded-Chain Pattern" - the prior row is marked, retained, and still individually retrievable.</summary>
    [Fact]
    public async Task A_resubmission_supersedes_the_prior_row_which_stays_permanently_retrievable()
    {
        var (client, context) = await SeedAsync();
        var assignment = await AssignmentEndpointsTests.PublishedAssignmentAsync(client, context);

        var first = await SubmissionWindowEndpointsTests.SubmitAsync(client, assignment.Id, context.StudentToken, "First attempt.");
        var second = await SubmissionWindowEndpointsTests.SubmitAsync(client, assignment.Id, context.StudentToken, "Second attempt.");

        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal("Submitted", second.Status);

        var refetchedFirst = await client.GetAndReadAsync<SubmissionDto>($"/api/v1/learning/submissions/{first.Id}", context.StudentToken);
        Assert.Equal("Superseded", refetchedFirst.Status);
        Assert.Equal(second.Id, refetchedFirst.SupersededBySubmissionId);
        Assert.Equal("First attempt.", refetchedFirst.TextContent);
    }

    /// <summary>edge-cases.md's residual note: an Assignment with resubmission disabled rejects a second attempt outright rather than creating a superseding one.</summary>
    [Fact]
    public async Task An_Assignment_with_resubmission_disabled_rejects_a_second_attempt_outright()
    {
        var (client, context) = await SeedAsync();
        var assignment = await AssignmentEndpointsTests.PublishedAssignmentAsync(
            client,
            context,
            AssignmentEndpointsTests.NewAssignment(context.CourseOfferingId, allowResubmission: false));

        await SubmissionWindowEndpointsTests.SubmitAsync(client, assignment.Id, context.StudentToken, "Only attempt.");

        var response = await client.PostAsync(
            $"/api/v1/learning/assignments/{assignment.Id}/submissions",
            context.StudentToken,
            new CreateSubmissionRequest("Second attempt.", null));

        await response.AssertProblemCodeAsync(HttpStatusCode.Conflict, "submission.resubmission_not_allowed");
    }

    /// <summary>LRN-10: the grading queue defaults to the counted attempt per Student, with the full retained chain available on request.</summary>
    [Fact]
    public async Task The_grading_queue_shows_the_counted_attempt_per_Student_by_default_and_the_whole_chain_on_request()
    {
        var (client, context) = await SeedAsync();
        var assignment = await AssignmentEndpointsTests.PublishedAssignmentAsync(client, context);

        await SubmissionWindowEndpointsTests.SubmitAsync(client, assignment.Id, context.StudentToken, "First.");
        await SubmissionWindowEndpointsTests.SubmitAsync(client, assignment.Id, context.StudentToken, "Second.");
        await SubmissionWindowEndpointsTests.SubmitAsync(client, assignment.Id, context.SecondStudentToken, "Other student.");

        var counted = await client.GetAndReadAsync<List<SubmissionDto>>(
            $"/api/v1/learning/assignments/{assignment.Id}/submissions",
            context.InstructorToken);
        var full = await client.GetAndReadAsync<List<SubmissionDto>>(
            $"/api/v1/learning/assignments/{assignment.Id}/submissions?includeSuperseded=true",
            context.InstructorToken);

        Assert.Equal(2, counted.Count);
        Assert.All(counted, s => Assert.Equal("Submitted", s.Status));
        Assert.Equal(3, full.Count);
        Assert.Single(full, s => s.Status == "Superseded");
    }

    [Fact]
    public async Task A_Student_may_read_their_own_Submission_but_never_another_Students()
    {
        var (client, context) = await SeedAsync();
        var assignment = await AssignmentEndpointsTests.PublishedAssignmentAsync(client, context);
        var mine = await SubmissionWindowEndpointsTests.SubmitAsync(client, assignment.Id, context.StudentToken, "Mine.");

        var own = await client.GetAsync($"/api/v1/learning/submissions/{mine.Id}", context.StudentToken);
        var theirs = await client.GetAsync($"/api/v1/learning/submissions/{mine.Id}", context.SecondStudentToken);

        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        await theirs.AssertProblemCodeAsync(HttpStatusCode.Forbidden, "submission.forbidden");
    }

    /// <summary>
    /// LRN-11: evaluation records the score, writes a synchronous Audit entry, and publishes
    /// <c>SubmissionEvaluated</c> to Learning's OWN outbox - a one-directional fan-out. Nothing here
    /// writes to Academic (design-decisions.md, "Cross-Module Feed of Assignment Scores into
    /// Academic's Grade").
    /// </summary>
    [Fact]
    public async Task Evaluating_records_the_score_audits_it_and_publishes_the_fan_out_event()
    {
        var (client, context) = await SeedAsync();
        var assignment = await AssignmentEndpointsTests.PublishedAssignmentAsync(client, context);
        var submission = await SubmissionWindowEndpointsTests.SubmitAsync(client, assignment.Id, context.StudentToken, "My answer.");

        var evaluated = await client.PostAndReadAsync<SubmissionDto>(
            $"/api/v1/learning/submissions/{submission.Id}/evaluate",
            context.InstructorToken,
            new EvaluateSubmissionRequest(88m, "Strong argument, weak conclusion."));

        Assert.Equal(88m, evaluated.Score!.RawPoints);
        Assert.Equal(88m, evaluated.Score.AwardedPoints);
        Assert.Equal(100, evaluated.Score.MaxPoints);
        Assert.Equal("Strong argument, weak conclusion.", evaluated.Score.Feedback);

        Assert.Equal(1, await AuditEntryCounter.CountAsync(fixture, "Submission", submission.Id.ToString(), "evaluate"));
        Assert.Equal(1, await AuditEntryCounter.CountOutboxAsync(fixture, "SubmissionEvaluated", submission.Id.ToString()));
    }

    [Fact]
    public async Task A_late_submissions_evaluation_applies_the_penalty_frozen_at_submit_time()
    {
        var (client, context) = await SeedAsync();
        var assignment = await AssignmentEndpointsTests.PublishedAssignmentAsync(
            client,
            context,
            AssignmentEndpointsTests.NewAssignment(
                context.CourseOfferingId,
                deadlineFromNow: TimeSpan.FromHours(-2),
                gracePeriod: TimeSpan.Zero,
                hardCloseFromNow: TimeSpan.FromDays(3),
                tiers: [new LatePenaltyTierDto(TimeSpan.FromHours(24), 50m)]));

        var submission = await SubmissionWindowEndpointsTests.SubmitAsync(client, assignment.Id, context.StudentToken, "Late.");

        var evaluated = await client.PostAndReadAsync<SubmissionDto>(
            $"/api/v1/learning/submissions/{submission.Id}/evaluate",
            context.InstructorToken,
            new EvaluateSubmissionRequest(80m, null));

        Assert.Equal(80m, evaluated.Score!.RawPoints);
        Assert.Equal(40m, evaluated.Score.AwardedPoints);
        Assert.Equal(50m, evaluated.Score.AppliedLatePenaltyPercentage);
    }

    [Fact]
    public async Task An_Instructor_who_does_not_own_the_offering_cannot_evaluate_its_Submissions()
    {
        var (client, context) = await SeedAsync();
        var otherInstructorToken = await (await ScenarioAsync()).UnrelatedInstructorTokenAsync();
        var assignment = await AssignmentEndpointsTests.PublishedAssignmentAsync(client, context);
        var submission = await SubmissionWindowEndpointsTests.SubmitAsync(client, assignment.Id, context.StudentToken, "My answer.");

        var response = await client.PostAsync(
            $"/api/v1/learning/submissions/{submission.Id}/evaluate",
            otherInstructorToken,
            new EvaluateSubmissionRequest(100m, null));

        await response.AssertProblemCodeAsync(HttpStatusCode.Forbidden, "submission.not_assigned_instructor");
    }

    [Fact]
    public async Task Evaluating_a_superseded_Submission_is_rejected_in_favour_of_the_counted_one()
    {
        var (client, context) = await SeedAsync();
        var assignment = await AssignmentEndpointsTests.PublishedAssignmentAsync(client, context);
        var first = await SubmissionWindowEndpointsTests.SubmitAsync(client, assignment.Id, context.StudentToken, "First.");
        await SubmissionWindowEndpointsTests.SubmitAsync(client, assignment.Id, context.StudentToken, "Second.");

        var response = await client.PostAsync(
            $"/api/v1/learning/submissions/{first.Id}/evaluate",
            context.InstructorToken,
            new EvaluateSubmissionRequest(50m, null));

        await response.AssertProblemCodeAsync(HttpStatusCode.Conflict, "submission.superseded");
    }

    /// <summary>One course context per test class, memoized - see <see cref="LearningScenario"/> for why this is not seeded per test.</summary>
    private async Task<(HttpClient Client, LearningTestDataSeeder.CourseContext Context)> SeedAsync()
    {
        var scenario = await LearningScenario.GetAsync(fixture, "submission-lifecycle");
        return (scenario.Client, scenario.Context);
    }

    private Task<LearningScenario> ScenarioAsync() => LearningScenario.GetAsync(fixture, "submission-lifecycle");
}
