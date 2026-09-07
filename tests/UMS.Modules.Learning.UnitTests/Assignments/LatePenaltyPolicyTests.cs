using UMS.Modules.Learning.Domain.Assignments;

namespace UMS.Modules.Learning.UnitTests.Assignments;

/// <summary>design-decisions.md "Late-Penalty &amp; Grace-Period Model": an ordered tier schedule where a one-tier policy IS a flat-percentage policy, so nothing is lost by generalizing.</summary>
public sealed class LatePenaltyPolicyTests
{
    private static LatePenaltyTier Tier(int hours, decimal deduction) =>
        LatePenaltyTier.Create(TimeSpan.FromHours(hours), deduction).Value;

    [Fact]
    public void NoDeduction_never_deducts_regardless_of_lateness()
    {
        Assert.Equal(0m, LatePenaltyPolicy.NoDeduction.DeductionFor(TimeSpan.FromDays(30)));
    }

    [Fact]
    public void A_single_tier_policy_behaves_exactly_like_a_flat_percentage_policy()
    {
        var policy = LatePenaltyPolicy.Create([Tier(24, 10m)]).Value;

        Assert.Equal(10m, policy.DeductionFor(TimeSpan.FromMinutes(1)));
        Assert.Equal(10m, policy.DeductionFor(TimeSpan.FromHours(24)));
        Assert.Equal(10m, policy.DeductionFor(TimeSpan.FromDays(7)));
    }

    [Fact]
    public void A_tiered_policy_resolves_the_first_tier_whose_bound_covers_the_lateness()
    {
        var policy = LatePenaltyPolicy.Create([Tier(24, 10m), Tier(72, 25m)]).Value;

        Assert.Equal(10m, policy.DeductionFor(TimeSpan.FromHours(1)));
        Assert.Equal(25m, policy.DeductionFor(TimeSpan.FromHours(25)));
    }

    [Fact]
    public void A_tier_boundary_is_inclusive()
    {
        var policy = LatePenaltyPolicy.Create([Tier(24, 10m), Tier(72, 25m)]).Value;

        Assert.Equal(10m, policy.DeductionFor(TimeSpan.FromHours(24)));
        Assert.Equal(25m, policy.DeductionFor(TimeSpan.FromHours(24).Add(TimeSpan.FromTicks(1))));
    }

    /// <summary>The last rung is the schedule's ceiling, not a cliff back to zero penalty.</summary>
    [Fact]
    public void Lateness_past_the_final_tier_carries_that_final_tiers_deduction()
    {
        var policy = LatePenaltyPolicy.Create([Tier(24, 10m), Tier(72, 25m)]).Value;

        Assert.Equal(25m, policy.DeductionFor(TimeSpan.FromDays(365)));
    }

    [Fact]
    public void A_non_late_submission_is_never_deducted()
    {
        var policy = LatePenaltyPolicy.Create([Tier(24, 10m)]).Value;

        Assert.Equal(0m, policy.DeductionFor(TimeSpan.Zero));
        Assert.Equal(0m, policy.DeductionFor(TimeSpan.FromHours(-1)));
    }

    [Fact]
    public void Tiers_that_are_not_strictly_ascending_by_lateness_are_rejected()
    {
        var result = LatePenaltyPolicy.Create([Tier(72, 10m), Tier(24, 25m)]);

        Assert.True(result.IsFailure);
        Assert.Equal("late_penalty_policy.tiers_not_ascending", result.Error!.Code);
    }

    /// <summary>A schedule that penalized a later submission LESS than an earlier one is nonsense - rejected at construction rather than silently mis-evaluated at submit time.</summary>
    [Fact]
    public void A_later_tier_that_deducts_less_than_an_earlier_one_is_rejected()
    {
        var result = LatePenaltyPolicy.Create([Tier(24, 25m), Tier(72, 10m)]);

        Assert.True(result.IsFailure);
        Assert.Equal("late_penalty_policy.deduction_decreases", result.Error!.Code);
    }

    [Fact]
    public void A_tier_with_a_non_positive_bound_is_rejected()
    {
        var result = LatePenaltyTier.Create(TimeSpan.Zero, 10m);

        Assert.True(result.IsFailure);
        Assert.Equal("late_penalty_tier.non_positive_max_lateness", result.Error!.Code);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void A_tier_deduction_outside_zero_to_one_hundred_is_rejected(int deduction)
    {
        var result = LatePenaltyTier.Create(TimeSpan.FromHours(1), deduction);

        Assert.True(result.IsFailure);
        Assert.Equal("late_penalty_tier.deduction_out_of_range", result.Error!.Code);
    }
}
