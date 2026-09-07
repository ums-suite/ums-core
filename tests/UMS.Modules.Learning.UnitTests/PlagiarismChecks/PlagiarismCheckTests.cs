using UMS.Modules.Learning.Domain.Events;
using UMS.Modules.Learning.Domain.PlagiarismChecks;
using UMS.Modules.Learning.Domain.Submissions;
using UMS.Modules.Learning.UnitTests.Submissions;

namespace UMS.Modules.Learning.UnitTests.PlagiarismChecks;

/// <summary>LRN-8/LRN-9 / design-decisions.md "PlagiarismCheck Execution Timing &amp; Resilience": early trigger, supersession-aware cancellation, a real terminal Failed status, and the retry path.</summary>
public sealed class PlagiarismCheckTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 5, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_newly_enqueued_check_starts_Queued_with_no_attempts()
    {
        var submission = SubmissionTests.Create(SubmissionTests.Published());

        var check = submission.EnqueuePlagiarismCheck(Now).Value;

        Assert.Equal(PlagiarismCheckStatus.Queued, check.Status);
        Assert.Equal(0, check.AttemptCount);
        Assert.Null(check.Score);
    }

    /// <summary>Idempotent: a duplicated relay pass over the same SubmissionCreated message must not produce a second provider call.</summary>
    [Fact]
    public void Enqueueing_twice_returns_the_same_in_flight_check_rather_than_stacking()
    {
        var submission = SubmissionTests.Create(SubmissionTests.Published());
        var first = submission.EnqueuePlagiarismCheck(Now).Value;

        var second = submission.EnqueuePlagiarismCheck(Now).Value;

        Assert.Same(first, second);
        Assert.Single(submission.PlagiarismChecks);
    }

    [Fact]
    public void Enqueueing_against_an_already_completed_check_is_rejected()
    {
        var submission = SubmissionTests.Create(SubmissionTests.Published());
        var check = submission.EnqueuePlagiarismCheck(Now).Value;
        check.Start(Now);
        check.Complete(Score(12m), Now);

        var result = submission.EnqueuePlagiarismCheck(Now);

        Assert.True(result.IsFailure);
        Assert.Equal("plagiarism_check.already_completed", result.Error!.Code);
    }

    [Fact]
    public void Enqueueing_against_a_superseded_Submission_is_rejected()
    {
        var submission = SubmissionTests.Create(SubmissionTests.Published());
        submission.Supersede(SubmissionId.New(), Now);

        var result = submission.EnqueuePlagiarismCheck(Now);

        Assert.True(result.IsFailure);
        Assert.Equal("plagiarism_check.submission_superseded", result.Error!.Code);
    }

    [Fact]
    public void Starting_a_check_increments_its_attempt_count()
    {
        var check = QueuedCheck(out _);

        check.Start(Now);

        Assert.Equal(PlagiarismCheckStatus.Running, check.Status);
        Assert.Equal(1, check.AttemptCount);
    }

    [Fact]
    public void Completing_a_Running_check_records_the_score()
    {
        var check = QueuedCheck(out _);
        check.Start(Now);

        var result = check.Complete(Score(42.5m), Now.AddSeconds(3));

        Assert.True(result.IsSuccess);
        Assert.Equal(PlagiarismCheckStatus.Completed, check.Status);
        Assert.Equal(42.5m, check.Score!.SimilarityPercentage);
        Assert.Null(check.FailureReason);
    }

    /// <summary>requirement-spec.md §2: "never silently treated as clean" - a Failed check has no score at all, which is distinguishable from a 0% similarity result.</summary>
    [Fact]
    public void A_failed_check_is_terminal_carries_a_reason_and_has_no_score()
    {
        var check = QueuedCheck(out _);
        check.Start(Now);

        var result = check.Fail("Circuit open after 3 consecutive provider failures.", Now.AddSeconds(9));

        Assert.True(result.IsSuccess);
        Assert.Equal(PlagiarismCheckStatus.Failed, check.Status);
        Assert.Null(check.Score);
        Assert.Equal("Circuit open after 3 consecutive provider failures.", check.FailureReason);
        Assert.True(check.IsTerminal);
    }

    [Fact]
    public void Completing_a_check_that_is_not_Running_is_rejected()
    {
        var check = QueuedCheck(out _);

        var result = check.Complete(Score(10m), Now);

        Assert.True(result.IsFailure);
        Assert.Equal("plagiarism_check.invalid_transition", result.Error!.Code);
    }

    /// <summary>design-decisions.md: supersession before completion discards the check, so metered provider quota is not spent on content a resubmission already made moot.</summary>
    [Fact]
    public void Superseding_a_Submission_cancels_its_still_running_check()
    {
        var submission = SubmissionTests.Create(SubmissionTests.Published());
        var check = submission.EnqueuePlagiarismCheck(Now).Value;
        check.Start(Now);

        submission.Supersede(SubmissionId.New(), Now.AddMinutes(1));

        Assert.Equal(PlagiarismCheckStatus.Cancelled, check.Status);
    }

    /// <summary>An already-Completed check keeps its result - the evidence stays attached to the attempt it actually describes.</summary>
    [Fact]
    public void Superseding_a_Submission_leaves_an_already_completed_check_intact()
    {
        var submission = SubmissionTests.Create(SubmissionTests.Published());
        var check = submission.EnqueuePlagiarismCheck(Now).Value;
        check.Start(Now);
        check.Complete(Score(31m), Now);

        submission.Supersede(SubmissionId.New(), Now.AddMinutes(1));

        Assert.Equal(PlagiarismCheckStatus.Completed, check.Status);
        Assert.Equal(31m, check.Score!.SimilarityPercentage);
    }

    [Fact]
    public void Retrying_re_arms_a_Failed_check_rather_than_creating_a_second_one()
    {
        var check = QueuedCheck(out var submission);
        check.Start(Now);
        check.Fail("Provider unreachable.", Now);

        var retried = submission.RetryPlagiarismCheck(Now.AddMinutes(10));

        Assert.True(retried.IsSuccess);
        Assert.Same(check, retried.Value);
        Assert.Equal(PlagiarismCheckStatus.Queued, check.Status);
        Assert.Equal(1, check.AttemptCount);
        Assert.Single(submission.PlagiarismChecks);
    }

    [Fact]
    public void Retrying_a_Completed_check_is_rejected()
    {
        var check = QueuedCheck(out var submission);
        check.Start(Now);
        check.Complete(Score(5m), Now);

        var retried = submission.RetryPlagiarismCheck(Now);

        Assert.True(retried.IsFailure);
        Assert.Equal("plagiarism_check.not_retryable", retried.Error!.Code);
    }

    [Fact]
    public void Retrying_a_Submission_with_no_check_yet_enqueues_the_first_one()
    {
        var submission = SubmissionTests.Create(SubmissionTests.Published());

        var retried = submission.RetryPlagiarismCheck(Now);

        Assert.True(retried.IsSuccess);
        Assert.Single(submission.PlagiarismChecks);
    }

    [Fact]
    public void A_completed_outcome_raises_PlagiarismCheckCompleted()
    {
        var check = QueuedCheck(out var submission);
        check.Start(Now);
        check.Complete(Score(18m), Now);
        submission.ClearDomainEvents();

        submission.RaisePlagiarismOutcome(check, Guid.NewGuid(), Now);

        Assert.Single(submission.DomainEvents.OfType<PlagiarismCheckCompleted>());
    }

    [Fact]
    public void A_failed_outcome_raises_PlagiarismCheckFailed()
    {
        var check = QueuedCheck(out var submission);
        check.Start(Now);
        check.Fail("Timed out.", Now);
        submission.ClearDomainEvents();

        submission.RaisePlagiarismOutcome(check, Guid.NewGuid(), Now);

        Assert.Single(submission.DomainEvents.OfType<PlagiarismCheckFailed>());
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(100.01)]
    public void A_similarity_percentage_outside_zero_to_one_hundred_is_rejected(decimal similarity)
    {
        var result = PlagiarismScore.Create(similarity, "Some sources.", "provider");

        Assert.True(result.IsFailure);
        Assert.Equal("plagiarism_score.out_of_range", result.Error!.Code);
    }

    [Fact]
    public void A_score_without_a_provider_name_is_rejected()
    {
        var result = PlagiarismScore.Create(10m, "Some sources.", "  ");

        Assert.True(result.IsFailure);
        Assert.Equal("plagiarism_score.provider_required", result.Error!.Code);
    }

    private static PlagiarismScore Score(decimal similarity) =>
        PlagiarismScore.Create(similarity, "3 matched sources.", "ums-fake-similarity-gateway").Value;

    private static PlagiarismCheck QueuedCheck(out Submission submission)
    {
        submission = SubmissionTests.Create(SubmissionTests.Published());
        return submission.EnqueuePlagiarismCheck(Now).Value;
    }
}
