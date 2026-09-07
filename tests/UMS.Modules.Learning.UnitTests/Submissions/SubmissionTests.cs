using UMS.Modules.Learning.Domain.Assignments;
using UMS.Modules.Learning.Domain.Events;
using UMS.Modules.Learning.Domain.PlagiarismChecks;
using UMS.Modules.Learning.Domain.Submissions;
using UMS.Modules.Learning.UnitTests.Assignments;

namespace UMS.Modules.Learning.UnitTests.Submissions;

/// <summary>LRN-6/LRN-7/LRN-11: immutability, the superseded chain, and evaluation (requirement-spec.md learning §4).</summary>
public sealed class SubmissionTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 5, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Student = Guid.NewGuid();
    private static readonly Guid StudentUser = Guid.NewGuid();

    [Fact]
    public void Creating_a_Submission_raises_SubmissionCreated_with_the_server_timestamp()
    {
        var assignment = Published();

        var submission = Create(assignment);

        Assert.Equal(Now, submission.SubmittedAt);
        var created = Assert.Single(submission.DomainEvents.OfType<SubmissionCreated>());
        Assert.Equal(Now, created.SubmittedAt);
    }

    [Fact]
    public void A_rejected_acceptance_verdict_never_produces_a_Submission()
    {
        var assignment = Published();
        var rejected = assignment.EvaluateAcceptance(Now.AddYears(5), Student);

        var result = Submission.Create(assignment, Student, StudentUser, "Late work.", [], rejected, Now.AddYears(5));

        Assert.True(result.IsFailure);
        Assert.Equal("submission.window_closed", result.Error!.Code);
    }

    [Fact]
    public void A_Submission_with_neither_text_nor_files_is_rejected()
    {
        var assignment = Published();

        var result = Submission.Create(assignment, Student, StudentUser, "   ", [], assignment.EvaluateAcceptance(Now, Student), Now);

        Assert.True(result.IsFailure);
        Assert.Equal("submission.empty", result.Error!.Code);
    }

    [Fact]
    public void A_text_only_Assignment_rejects_a_file_attachment()
    {
        var assignment = Published(AllowedSubmissionType.Text);

        var result = Submission.Create(assignment, Student, StudentUser, "Body.", [File()], assignment.EvaluateAcceptance(Now, Student), Now);

        Assert.True(result.IsFailure);
        Assert.Equal("submission.files_not_allowed", result.Error!.Code);
    }

    [Fact]
    public void A_file_only_Assignment_rejects_a_text_only_submission()
    {
        var assignment = Published(AllowedSubmissionType.File);

        var result = Submission.Create(assignment, Student, StudentUser, "Body.", [], assignment.EvaluateAcceptance(Now, Student), Now);

        Assert.True(result.IsFailure);
        Assert.Equal("submission.file_required", result.Error!.Code);
    }

    [Fact]
    public void A_SubmissionFile_must_reference_a_real_Documents_artifact()
    {
        var result = SubmissionFile.Create(Guid.Empty, "essay.pdf", "application/pdf");

        Assert.True(result.IsFailure);
        Assert.Equal("submission_file.artifact_required", result.Error!.Code);
    }

    /// <summary>design-decisions.md "Submission Immutability &amp; Resubmission via Superseded-Chain Pattern" - the prior attempt is marked, never mutated or removed.</summary>
    [Fact]
    public void Superseding_marks_the_prior_attempt_and_retains_its_content()
    {
        var assignment = Published();
        var first = Create(assignment, text: "First attempt.");
        var second = Create(assignment, text: "Second attempt.");

        var result = first.Supersede(second.Id, Now.AddHours(1));

        Assert.True(result.IsSuccess);
        Assert.Equal(SubmissionStatus.Superseded, first.Status);
        Assert.Equal(second.Id, first.SupersededBySubmissionId);
        Assert.Equal(Now.AddHours(1), first.SupersededAt);
        Assert.Equal("First attempt.", first.TextContent);
        Assert.Single(first.DomainEvents.OfType<SubmissionSuperseded>());
    }

    [Fact]
    public void Superseding_an_already_superseded_Submission_is_rejected()
    {
        var assignment = Published();
        var first = Create(assignment);
        first.Supersede(SubmissionId.New(), Now);

        var result = first.Supersede(SubmissionId.New(), Now);

        Assert.True(result.IsFailure);
        Assert.Equal("submission.invalid_transition", result.Error!.Code);
    }

    [Fact]
    public void Evaluating_records_the_score_and_raises_SubmissionEvaluated()
    {
        var assignment = Published();
        var submission = Create(assignment);
        submission.ClearDomainEvents();

        var result = submission.Evaluate(80m, 100, "Good work.", Guid.NewGuid(), Now.AddDays(1));

        Assert.True(result.IsSuccess);
        Assert.Equal(80m, submission.Score!.RawPoints);
        Assert.Equal(80m, submission.Score.AwardedPoints);
        Assert.Equal("Good work.", submission.Score.Feedback);
        Assert.Single(submission.DomainEvents.OfType<SubmissionEvaluated>());
    }

    /// <summary>The late penalty resolved at submit time is frozen on the Submission, so a later policy edit never silently re-scores an already-submitted attempt.</summary>
    [Fact]
    public void Evaluating_a_late_Submission_applies_the_penalty_frozen_at_submit_time()
    {
        var policy = LatePenaltyPolicy.Create([LatePenaltyTier.Create(TimeSpan.FromHours(24), 25m).Value]).Value;
        var assignment = AssignmentTests.Create(AssignmentTests.Window(Now, Now.AddHours(1), TimeSpan.Zero, policy, Now.AddDays(5)));
        assignment.Publish(Now);
        var lateAt = Now.AddHours(3);
        var submission = Submission.Create(assignment, Student, StudentUser, "Late.", [], assignment.EvaluateAcceptance(lateAt, Student), lateAt).Value;

        submission.Evaluate(80m, 100, null, Guid.NewGuid(), lateAt.AddDays(1));

        Assert.True(submission.IsLate);
        Assert.Equal(25m, submission.LatePenaltyPercentage);
        Assert.Equal(80m, submission.Score!.RawPoints);
        Assert.Equal(60m, submission.Score.AwardedPoints);
    }

    [Fact]
    public void Evaluating_a_superseded_Submission_is_rejected()
    {
        var assignment = Published();
        var submission = Create(assignment);
        submission.Supersede(SubmissionId.New(), Now);

        var result = submission.Evaluate(80m, 100, null, Guid.NewGuid(), Now);

        Assert.True(result.IsFailure);
        Assert.Equal("submission.superseded", result.Error!.Code);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void A_score_outside_zero_to_maxPoints_is_rejected(int points)
    {
        var assignment = Published();
        var submission = Create(assignment);

        var result = submission.Evaluate(points, 100, null, Guid.NewGuid(), Now);

        Assert.True(result.IsFailure);
        Assert.Equal("assignment_score.points_out_of_range", result.Error!.Code);
    }

    /// <summary>requirement-spec.md §4's headline invariant: a PlagiarismCheck NEVER gates evaluation - not even a Failed one.</summary>
    [Theory]
    [InlineData(PlagiarismCheckStatus.Queued)]
    [InlineData(PlagiarismCheckStatus.Running)]
    [InlineData(PlagiarismCheckStatus.Failed)]
    public void Evaluation_succeeds_regardless_of_the_PlagiarismChecks_status(PlagiarismCheckStatus status)
    {
        var assignment = Published();
        var submission = Create(assignment);
        var check = submission.EnqueuePlagiarismCheck(Now).Value;

        if (status is PlagiarismCheckStatus.Running or PlagiarismCheckStatus.Failed)
        {
            check.Start(Now);
        }

        if (status == PlagiarismCheckStatus.Failed)
        {
            check.Fail("Provider unreachable.", Now);
        }

        var result = submission.Evaluate(75m, 100, null, Guid.NewGuid(), Now.AddDays(1));

        Assert.True(result.IsSuccess);
        Assert.Equal(status, submission.LatestPlagiarismCheck!.Status);
    }

    internal static SubmissionFile File() =>
        SubmissionFile.Create(Guid.NewGuid(), "essay.pdf", "application/pdf").Value;

    internal static Assignment Published(AllowedSubmissionType allowedSubmissionType = AllowedSubmissionType.TextOrFile, bool allowResubmission = true)
    {
        var assignment = AssignmentTests.Create(
            AssignmentTests.Window(Now.AddDays(-1), Now.AddDays(9), TimeSpan.FromMinutes(5), LatePenaltyPolicy.NoDeduction, Now.AddDays(12)),
            allowedSubmissionType,
            allowResubmission);
        assignment.Publish(Now.AddDays(-1));
        return assignment;
    }

    internal static Submission Create(Assignment assignment, string? text = "My answer.", Guid? studentId = null) =>
        Submission.Create(
            assignment,
            studentId ?? Student,
            StudentUser,
            text,
            [],
            assignment.EvaluateAcceptance(Now, studentId ?? Student),
            Now).Value;
}
