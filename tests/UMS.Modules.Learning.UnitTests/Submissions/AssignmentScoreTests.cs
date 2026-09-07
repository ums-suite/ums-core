using UMS.Modules.Learning.Domain.Submissions;
using UMS.Modules.Learning.UnitTests.Assignments;

namespace UMS.Modules.Learning.UnitTests.Submissions;

/// <summary>
/// <c>AssignmentScore</c> is only ever produced through <c>Submission.Evaluate()</c> - never a raw
/// field write (requirement-spec.md learning §2) - so these tests drive it through that method,
/// which is also what keeps its <c>internal</c> factory honest about the aggregate being the only
/// way in.
/// </summary>
public sealed class AssignmentScoreTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 5, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void An_on_time_submission_awards_exactly_what_the_Instructor_entered()
    {
        var submission = OnTimeSubmission();

        submission.Evaluate(87.5m, 100, "Well argued.", Guid.NewGuid(), Now);

        Assert.Equal(87.5m, submission.Score!.RawPoints);
        Assert.Equal(87.5m, submission.Score.AwardedPoints);
        Assert.Equal(0m, submission.Score.AppliedLatePenaltyPercentage);
    }

    /// <summary>Keeping both the raw and awarded figures means a Student disputing a deduction can see exactly what was marked and exactly what was subtracted, not only the net.</summary>
    [Fact]
    public void Both_the_raw_and_the_penalty_adjusted_figures_are_retained()
    {
        var submission = LateSubmission(penaltyPercentage: 40m);

        submission.Evaluate(90m, 100, null, Guid.NewGuid(), Now);

        Assert.Equal(90m, submission.Score!.RawPoints);
        Assert.Equal(54m, submission.Score.AwardedPoints);
        Assert.Equal(40m, submission.Score.AppliedLatePenaltyPercentage);
    }

    [Fact]
    public void A_hundred_percent_penalty_tier_awards_zero_but_still_records_what_was_marked()
    {
        var submission = LateSubmission(penaltyPercentage: 100m);

        submission.Evaluate(72m, 100, "Accepted for the record only.", Guid.NewGuid(), Now);

        Assert.Equal(72m, submission.Score!.RawPoints);
        Assert.Equal(0m, submission.Score.AwardedPoints);
    }

    [Fact]
    public void The_awarded_figure_is_rounded_to_two_decimal_places()
    {
        var submission = LateSubmission(penaltyPercentage: 33m);

        submission.Evaluate(10m, 100, null, Guid.NewGuid(), Now);

        Assert.Equal(6.70m, submission.Score!.AwardedPoints);
    }

    [Fact]
    public void Feedback_is_optional_and_normalizes_to_an_empty_string()
    {
        var submission = OnTimeSubmission();

        submission.Evaluate(50m, 100, null, Guid.NewGuid(), Now);

        Assert.Equal(string.Empty, submission.Score!.Feedback);
    }

    private static Submission OnTimeSubmission() => SubmissionTests.Create(SubmissionTests.Published());

    private static Submission LateSubmission(decimal penaltyPercentage)
    {
        var policy = UMS.Modules.Learning.Domain.Assignments.LatePenaltyPolicy.Create(
        [
            UMS.Modules.Learning.Domain.Assignments.LatePenaltyTier.Create(TimeSpan.FromHours(24), penaltyPercentage).Value,
        ]).Value;

        var assignment = AssignmentTests.Create(AssignmentTests.Window(Now.AddDays(-2), Now.AddDays(-1), TimeSpan.Zero, policy, Now.AddDays(5)));
        assignment.Publish(Now.AddDays(-2));

        var studentId = Guid.NewGuid();
        return Submission.Create(assignment, studentId, Guid.NewGuid(), "Late answer.", [], assignment.EvaluateAcceptance(Now, studentId), Now).Value;
    }
}
