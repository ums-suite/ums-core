using UMS.Modules.Admission.Domain.ExamAttempts;
using UMS.Modules.Admission.Domain.Tests;

namespace UMS.Modules.Admission.UnitTests.ExamAttempts;

/// <summary>ADM-11..14: requirement-spec.md §4's single-submission invariant, ADR-0018's flag-never-auto-disqualifies posture.</summary>
public sealed class ExamAttemptTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static ExamAttempt Start(int timeLimitMinutes = 60) =>
        ExamAttempt.Start(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "R-001", seedValue: 42, [new QuestionId(Guid.NewGuid())], timeLimitMinutes, Now).Value;

    [Fact]
    public void Starting_with_no_selected_questions_is_rejected()
    {
        var result = ExamAttempt.Start(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "R-001", 42, [], 60, Now);

        Assert.True(result.IsFailure);
        Assert.Equal("exam_attempt.questions_required", result.Error!.Code);
    }

    [Fact]
    public void An_answer_saved_after_expiry_is_silently_ignored()
    {
        var attempt = Start(timeLimitMinutes: 30);
        var questionId = attempt.SelectedQuestionIds.Single();

        attempt.SaveAnswer(questionId, 1, null, Now.AddMinutes(31));

        Assert.Empty(attempt.Answers);
    }

    [Fact]
    public void An_answer_for_a_question_outside_the_selection_is_ignored()
    {
        var attempt = Start();

        attempt.SaveAnswer(new QuestionId(Guid.NewGuid()), 0, null, Now.AddMinutes(1));

        Assert.Empty(attempt.Answers);
    }

    [Fact]
    public void Submitting_transitions_to_Submitted_and_raises_one_event()
    {
        var attempt = Start();

        attempt.Submit("manual", Now.AddMinutes(5));

        Assert.Equal(ExamAttemptStatus.Submitted, attempt.Status);
        Assert.Equal("manual", attempt.SubmissionSource);
        Assert.Single(attempt.DomainEvents);
    }

    [Fact]
    public void Evaluation_before_submission_is_rejected()
    {
        var attempt = Start();

        var result = attempt.RecordEvaluation(10, 5, Now);

        Assert.True(result.IsFailure);
        Assert.Equal("exam_attempt.not_submitted", result.Error!.Code);
    }

    [Fact]
    public void Raising_an_integrity_flag_never_mutates_the_attempts_own_status()
    {
        var attempt = Start();

        attempt.RaiseIntegrityFlag("TabSwitch", "switched tabs 3 times", 0.8m, Now);

        Assert.Equal(ExamAttemptStatus.InProgress, attempt.Status);
        Assert.Single(attempt.IntegrityFlags);
        Assert.Equal(IntegrityFlagOutcome.Pending, attempt.IntegrityFlags.Single().Outcome);
    }

    [Fact]
    public void A_human_review_records_the_outcome_without_altering_attempt_status()
    {
        var attempt = Start();
        var flag = attempt.RaiseIntegrityFlag("TabSwitch", "switched tabs", 0.8m, Now);

        var result = attempt.ReviewIntegrityFlag(flag.Id, IntegrityFlagOutcome.Cleared, Guid.NewGuid(), "benign, network blip", Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(IntegrityFlagOutcome.Cleared, attempt.IntegrityFlags.Single().Outcome);
        Assert.Equal(ExamAttemptStatus.InProgress, attempt.Status);
    }
}
