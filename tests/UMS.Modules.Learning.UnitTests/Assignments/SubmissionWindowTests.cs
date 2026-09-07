using UMS.Modules.Learning.Domain.Assignments;

namespace UMS.Modules.Learning.UnitTests.Assignments;

/// <summary>design-decisions.md "Late-Penalty &amp; Grace-Period Model for <c>SubmissionWindow</c>" - the grace period is folded INTO the effective deadline, not evaluated as a separate later rule.</summary>
public sealed class SubmissionWindowTests
{
    private static readonly DateTimeOffset Opens = new(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Deadline = new(2026, 3, 10, 23, 59, 0, TimeSpan.Zero);

    [Fact]
    public void EffectiveDeadline_is_the_stated_deadline_plus_the_grace_period()
    {
        var window = SubmissionWindow.Create(Opens, Deadline, TimeSpan.FromMinutes(5), LatePenaltyPolicy.NoDeduction, Deadline.AddDays(3)).Value;

        Assert.Equal(Deadline.AddMinutes(5), window.EffectiveDeadline);
    }

    [Fact]
    public void A_zero_grace_period_is_allowed_and_makes_the_effective_deadline_the_stated_one()
    {
        var window = SubmissionWindow.Create(Opens, Deadline, TimeSpan.Zero, LatePenaltyPolicy.NoDeduction, Deadline).Value;

        Assert.Equal(Deadline, window.EffectiveDeadline);
    }

    [Fact]
    public void A_deadline_at_or_before_the_opening_instant_is_rejected()
    {
        var result = SubmissionWindow.Create(Opens, Opens, TimeSpan.Zero, LatePenaltyPolicy.NoDeduction, Opens.AddDays(1));

        Assert.True(result.IsFailure);
        Assert.Equal("submission_window.deadline_before_open", result.Error!.Code);
    }

    [Fact]
    public void A_negative_grace_period_is_rejected()
    {
        var result = SubmissionWindow.Create(Opens, Deadline, TimeSpan.FromMinutes(-5), LatePenaltyPolicy.NoDeduction, Deadline.AddDays(1));

        Assert.True(result.IsFailure);
        Assert.Equal("submission_window.negative_grace_period", result.Error!.Code);
    }

    /// <summary>A hardCloseAt earlier than the effective deadline would make the grace period unreachable - a silently self-contradicting window, rejected at construction.</summary>
    [Fact]
    public void A_hard_close_before_the_effective_deadline_is_rejected()
    {
        var result = SubmissionWindow.Create(Opens, Deadline, TimeSpan.FromMinutes(30), LatePenaltyPolicy.NoDeduction, Deadline.AddMinutes(10));

        Assert.True(result.IsFailure);
        Assert.Equal("submission_window.hard_close_before_effective_deadline", result.Error!.Code);
    }

    [Fact]
    public void A_hard_close_exactly_at_the_effective_deadline_is_accepted()
    {
        var result = SubmissionWindow.Create(Opens, Deadline, TimeSpan.FromMinutes(30), LatePenaltyPolicy.NoDeduction, Deadline.AddMinutes(30));

        Assert.True(result.IsSuccess);
    }
}
