using UMS.Modules.Learning.Domain.Assignments;

namespace UMS.Modules.Learning.UnitTests.Assignments;

/// <summary>
/// The heart of LRN-6: <c>Assignment.EvaluateAcceptance</c>'s window boundaries, exactly as
/// design-decisions.md's "Late-Penalty &amp; Grace-Period Model" orders them - status gate, open
/// gate, <c>deadline + gracePeriod</c>, tier schedule, <c>hardCloseAt</c> - with any applicable
/// <c>SubmissionExtension</c> taking the later of its own deadline and the base hard close.
///
/// <para>
/// Note what these tests do NOT contain: any concurrency scenario. design-decisions.md's "Why
/// Submission Timing Doesn't Need Seat-Limit-Style Concurrency Control" resolves that this
/// evaluation contends over nothing - no counter, no capacity, no shared mutable state - so there
/// is no seat-limit-style race here to test, and inventing one would be testing a mechanism this
/// module deliberately does not have (contrast Academic's own
/// <c>EnrollmentConcurrencyTests</c>, which tests a genuinely scarce resource).
/// </para>
/// </summary>
public sealed class SubmissionAcceptanceTests
{
    private static readonly DateTimeOffset Opens = new(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Deadline = new(2026, 3, 10, 23, 59, 0, TimeSpan.Zero);
    private static readonly TimeSpan Grace = TimeSpan.FromMinutes(5);
    private static readonly DateTimeOffset HardClose = Deadline.AddDays(3);

    private static readonly Guid Student = Guid.NewGuid();

    [Fact]
    public void A_Draft_Assignment_rejects_every_submission()
    {
        var assignment = Published(publish: false);

        var acceptance = assignment.EvaluateAcceptance(Deadline.AddHours(-1), Student);

        Assert.False(acceptance.IsAccepted);
        Assert.Equal("submission.assignment_not_published", acceptance.RejectionCode);
    }

    [Fact]
    public void A_Cancelled_Assignment_rejects_every_submission()
    {
        var assignment = Published();
        assignment.Cancel("Offering withdrawn.", Opens.AddDays(1));

        var acceptance = assignment.EvaluateAcceptance(Deadline.AddHours(-1), Student);

        Assert.False(acceptance.IsAccepted);
        Assert.Equal("submission.assignment_not_published", acceptance.RejectionCode);
    }

    [Fact]
    public void A_submission_before_the_window_opens_is_rejected()
    {
        var assignment = Published();

        var acceptance = assignment.EvaluateAcceptance(Opens.AddMinutes(-1), Student);

        Assert.False(acceptance.IsAccepted);
        Assert.Equal("submission.window_not_open", acceptance.RejectionCode);
    }

    [Fact]
    public void A_submission_before_the_stated_deadline_is_on_time_with_no_penalty()
    {
        var assignment = Published();

        var acceptance = assignment.EvaluateAcceptance(Deadline.AddMinutes(-1), Student);

        Assert.True(acceptance.IsAccepted);
        Assert.False(acceptance.IsLate);
        Assert.Equal(0m, acceptance.LatePenaltyPercentage);
    }

    /// <summary>edge-cases.md's whole point: an upload that finishes a couple of minutes past the stated deadline lands inside the disclosed grace period and counts as on time, with no penalty at all.</summary>
    [Fact]
    public void A_submission_inside_the_grace_period_is_still_on_time_with_no_penalty()
    {
        var assignment = Published();

        var acceptance = assignment.EvaluateAcceptance(Deadline.AddMinutes(2), Student);

        Assert.True(acceptance.IsAccepted);
        Assert.False(acceptance.IsLate);
        Assert.Equal(0m, acceptance.LatePenaltyPercentage);
    }

    [Fact]
    public void A_submission_exactly_at_the_effective_deadline_is_still_on_time()
    {
        var assignment = Published();

        var acceptance = assignment.EvaluateAcceptance(Deadline + Grace, Student);

        Assert.True(acceptance.IsAccepted);
        Assert.False(acceptance.IsLate);
    }

    [Fact]
    public void A_submission_just_past_the_grace_period_lands_in_the_first_penalty_tier()
    {
        var assignment = Published();

        var acceptance = assignment.EvaluateAcceptance(Deadline + Grace + TimeSpan.FromMinutes(1), Student);

        Assert.True(acceptance.IsAccepted);
        Assert.True(acceptance.IsLate);
        Assert.Equal(10m, acceptance.LatePenaltyPercentage);
    }

    [Fact]
    public void A_submission_past_the_first_tier_lands_in_the_second()
    {
        var assignment = Published();

        var acceptance = assignment.EvaluateAcceptance(Deadline + Grace + TimeSpan.FromHours(30), Student);

        Assert.True(acceptance.IsAccepted);
        Assert.True(acceptance.IsLate);
        Assert.Equal(25m, acceptance.LatePenaltyPercentage);
    }

    [Fact]
    public void A_submission_exactly_at_hardCloseAt_is_still_accepted()
    {
        var assignment = Published();

        var acceptance = assignment.EvaluateAcceptance(HardClose, Student);

        Assert.True(acceptance.IsAccepted);
        Assert.True(acceptance.IsLate);
    }

    [Fact]
    public void A_submission_past_hardCloseAt_is_rejected_outright_at_any_penalty_level()
    {
        var assignment = Published();

        var acceptance = assignment.EvaluateAcceptance(HardClose.AddSeconds(1), Student);

        Assert.False(acceptance.IsAccepted);
        Assert.Equal("submission.window_closed", acceptance.RejectionCode);
    }

    /// <summary>requirement-spec.md §4: an extension is scoped to exactly one (Assignment, Student) pair and never widens the window for anyone else.</summary>
    [Fact]
    public void An_extension_widens_acceptance_for_that_Student_only()
    {
        var assignment = Published();
        var otherStudent = Guid.NewGuid();
        assignment.GrantExtension(Student, Guid.NewGuid(), HardClose.AddDays(2), Guid.NewGuid(), "Hospitalized.", waivesLatePenalty: false, Opens.AddDays(1));

        var extended = assignment.EvaluateAcceptance(HardClose.AddDays(1), Student);
        var unaffected = assignment.EvaluateAcceptance(HardClose.AddDays(1), otherStudent);

        Assert.True(extended.IsAccepted);
        Assert.False(unaffected.IsAccepted);
        Assert.Equal("submission.window_closed", unaffected.RejectionCode);
    }

    [Fact]
    public void An_extension_still_applies_the_late_penalty_unless_the_grant_waived_it()
    {
        var assignment = Published();
        assignment.GrantExtension(Student, Guid.NewGuid(), HardClose.AddDays(2), Guid.NewGuid(), "Family emergency.", waivesLatePenalty: false, Opens.AddDays(1));

        var acceptance = assignment.EvaluateAcceptance(HardClose.AddDays(1), Student);

        Assert.True(acceptance.IsAccepted);
        Assert.True(acceptance.IsLate);
        Assert.Equal(25m, acceptance.LatePenaltyPercentage);
    }

    /// <summary>edge-cases.md's residual note: "a genuine accommodation and an 'I'll give you a few extra hours but it's still late' grant are both legitimate, distinct Instructor intents".</summary>
    [Fact]
    public void An_extension_that_waives_the_late_penalty_produces_a_zero_deduction()
    {
        var assignment = Published();
        assignment.GrantExtension(Student, Guid.NewGuid(), HardClose.AddDays(2), Guid.NewGuid(), "Documented university outage.", waivesLatePenalty: true, Opens.AddDays(1));

        var acceptance = assignment.EvaluateAcceptance(HardClose.AddDays(1), Student);

        Assert.True(acceptance.IsAccepted);
        Assert.True(acceptance.IsLate);
        Assert.Equal(0m, acceptance.LatePenaltyPercentage);
    }

    /// <summary>The accept check takes the LATER of the base hard close and the extension - an extension is never allowed to accidentally NARROW a Student's window.</summary>
    [Fact]
    public void An_extension_earlier_than_the_base_hard_close_never_narrows_the_window()
    {
        var assignment = Published();
        assignment.GrantExtension(Student, Guid.NewGuid(), Deadline.AddDays(1), Guid.NewGuid(), "Mis-entered date.", waivesLatePenalty: false, Opens.AddDays(1));

        var acceptance = assignment.EvaluateAcceptance(HardClose, Student);

        Assert.True(acceptance.IsAccepted);
    }

    [Fact]
    public void An_accepted_submission_records_which_extension_widened_it()
    {
        var assignment = Published();
        var granted = assignment.GrantExtension(Student, Guid.NewGuid(), HardClose.AddDays(2), Guid.NewGuid(), "Accommodation.", waivesLatePenalty: false, Opens.AddDays(1));

        var acceptance = assignment.EvaluateAcceptance(HardClose.AddDays(1), Student);

        Assert.Equal(granted.Value.Id, acceptance.AppliedExtensionId);
    }

    private static Assignment Published(bool publish = true)
    {
        var policy = LatePenaltyPolicy.Create(
        [
            LatePenaltyTier.Create(TimeSpan.FromHours(24), 10m).Value,
            LatePenaltyTier.Create(TimeSpan.FromHours(72), 25m).Value,
        ]).Value;

        var assignment = AssignmentTests.Create(AssignmentTests.Window(Opens, Deadline, Grace, policy, HardClose));
        if (publish)
        {
            assignment.Publish(Opens);
        }

        return assignment;
    }
}
