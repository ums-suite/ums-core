using UMS.Modules.Hostel.Domain.Applications;
using UMS.Modules.Hostel.Domain.Hostels;

namespace UMS.Modules.Hostel.UnitTests.Applications;

/// <summary>HOS-3..6: requirement-spec.md §2/§3/§4; design-decisions.md "HostelApplication Withdrawal as a First-Class State Transition".</summary>
public sealed class HostelApplicationTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static HostelApplication Draft(int yearOfStudy = 2, bool hasFinancialNeed = false, decimal? homeDistrictDistanceKm = null) =>
        HostelApplication.CreateDraft(Guid.NewGuid(), Guid.NewGuid(), yearOfStudy, hasFinancialNeed, homeDistrictDistanceKm, Now).Value;

    private static HostelPreference Preference(int rank = 1) => HostelPreference.Create(Guid.NewGuid(), RoomType.SingleOccupancy, rank).Value;

    [Fact]
    public void Submitting_without_any_preference_is_rejected()
    {
        var application = Draft();

        var result = application.Submit(Now);

        Assert.True(result.IsFailure);
        Assert.Equal("hostel_application.no_preferences", result.Error!.Code);
    }

    [Fact]
    public void Submitting_with_a_preference_transitions_to_Submitted_and_raises_the_event()
    {
        var application = Draft();
        application.ReplacePreferences([Preference()]);

        var result = application.Submit(Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(HostelApplicationStatus.Submitted, application.Status);
        Assert.Contains(application.DomainEvents, e => e is UMS.Modules.Hostel.Domain.Events.HostelApplicationSubmitted);
    }

    [Fact]
    public void Duplicate_preference_ranks_are_rejected()
    {
        var application = Draft();

        var result = application.ReplacePreferences([Preference(1), Preference(1)]);

        Assert.True(result.IsFailure);
        Assert.Equal("hostel_application.duplicate_preference_rank", result.Error!.Code);
    }

    [Fact]
    public void Approving_without_a_passing_eligibility_result_is_rejected()
    {
        var application = Draft();
        application.ReplacePreferences([Preference()]);
        application.Submit(Now);
        application.MarkRanked(score: 10, isEligible: false, rankPosition: null, Now);

        var result = application.Approve(Now);

        Assert.True(result.IsFailure);
        Assert.Equal("hostel_application.not_eligible", result.Error!.Code);
    }

    [Fact]
    public void Approving_an_eligible_Ranked_application_succeeds_and_raises_the_event()
    {
        var application = Draft();
        application.ReplacePreferences([Preference()]);
        application.Submit(Now);
        application.MarkRanked(score: 10, isEligible: true, rankPosition: 1, Now);

        var result = application.Approve(Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(HostelApplicationStatus.Approved, application.Status);
        Assert.Contains(application.DomainEvents, e => e is UMS.Modules.Hostel.Domain.Events.HostelApplicationApproved);
    }

    [Theory]
    [InlineData(HostelApplicationStatus.Draft)]
    [InlineData(HostelApplicationStatus.Submitted)]
    [InlineData(HostelApplicationStatus.Waitlisted)]
    public void Withdraw_succeeds_from_every_non_terminal_non_approved_state(HostelApplicationStatus status)
    {
        var application = Draft();
        application.ReplacePreferences([Preference()]);
        if (status is HostelApplicationStatus.Submitted or HostelApplicationStatus.Waitlisted)
        {
            application.Submit(Now);
        }

        if (status == HostelApplicationStatus.Waitlisted)
        {
            application.MarkRanked(score: 5, isEligible: true, rankPosition: 1, Now);
            application.Waitlist(Now);
        }

        var result = application.Withdraw(Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(HostelApplicationStatus.Withdrawn, application.Status);
        Assert.Contains(application.DomainEvents, e => e is UMS.Modules.Hostel.Domain.Events.HostelApplicationWithdrawn);
    }

    [Fact]
    public void Withdraw_is_rejected_once_Approved_edge_case_HostelApplication_withdrawn_while_its_allocation_is_mid_approval()
    {
        var application = Draft();
        application.ReplacePreferences([Preference()]);
        application.Submit(Now);
        application.MarkRanked(score: 5, isEligible: true, rankPosition: 1, Now);
        application.Approve(Now);

        var result = application.Withdraw(Now);

        Assert.True(result.IsFailure);
        Assert.Equal("hostel_application.not_withdrawable", result.Error!.Code);
        Assert.Equal(HostelApplicationStatus.Approved, application.Status);
    }
}
