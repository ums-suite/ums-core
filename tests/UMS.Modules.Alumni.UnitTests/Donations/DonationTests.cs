using UMS.Modules.Alumni.Domain.Donations;

namespace UMS.Modules.Alumni.UnitTests.Donations;

/// <summary>
/// requirement-spec.md §2.4/§4: never Confirmed except via Finance's signal; anonymity is
/// display-only; design-decisions.md "Recurring-Donation Retry/Dunning Policy" - pause on first
/// failure, never retry.
/// </summary>
public sealed class DonationTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Initiate_starts_Pending_and_never_Confirmed()
    {
        var donation = Donation.Initiate(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 500m, "BDT", isAnonymous: false, RecurrenceInterval.None, Now);

        Assert.Equal(DonationStatus.Pending, donation.Status);
        Assert.Empty(donation.DomainEvents);
    }

    [Fact]
    public void Initiate_recurring_schedules_the_first_NextChargeAt_immediately()
    {
        var donation = Donation.Initiate(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 500m, "BDT", isAnonymous: false, RecurrenceInterval.Monthly, Now);

        Assert.NotNull(donation.NextChargeAt);
        Assert.Equal(Now.AddMonths(1), donation.NextChargeAt);
        Assert.Equal(RecurrenceStatus.Active, donation.RecurrenceStatus);
    }

    [Fact]
    public void Confirm_transitions_Pending_to_Confirmed_and_raises_DonationConfirmed()
    {
        var donation = Donation.Initiate(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 500m, "BDT", isAnonymous: true, RecurrenceInterval.None, Now);

        donation.Confirm(Now.AddMinutes(5));

        Assert.Equal(DonationStatus.Confirmed, donation.Status);
        Assert.Single(donation.DomainEvents);
    }

    [Fact]
    public void IsAnonymous_flag_never_hides_the_real_AlumnusId_link()
    {
        var alumnusId = Guid.NewGuid();
        var donation = Donation.Initiate(alumnusId, Guid.NewGuid(), Guid.NewGuid(), 500m, "BDT", isAnonymous: true, RecurrenceInterval.None, Now);

        Assert.Equal(alumnusId, donation.AlumnusId);
        Assert.True(donation.IsAnonymous);
    }

    /// <summary>design-decisions.md: the FIRST failure pauses immediately - no automated retry.</summary>
    [Fact]
    public void Fail_on_a_recurring_root_pauses_immediately_and_clears_NextChargeAt()
    {
        var donation = Donation.Initiate(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 500m, "BDT", isAnonymous: false, RecurrenceInterval.Monthly, Now);

        donation.Fail(Now.AddMinutes(5));

        Assert.Equal(DonationStatus.Failed, donation.Status);
        Assert.Equal(RecurrenceStatus.Paused, donation.RecurrenceStatus);
        Assert.Null(donation.NextChargeAt);
    }

    [Fact]
    public void Fail_on_a_one_off_Donation_does_not_touch_RecurrenceStatus()
    {
        var donation = Donation.Initiate(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 500m, "BDT", isAnonymous: false, RecurrenceInterval.None, Now);

        donation.Fail(Now.AddMinutes(5));

        Assert.Equal(DonationStatus.Failed, donation.Status);
        Assert.Null(donation.RecurrenceStatus);
    }

    [Fact]
    public void CancelRecurring_on_a_non_recurring_Donation_throws()
    {
        var donation = Donation.Initiate(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 500m, "BDT", isAnonymous: false, RecurrenceInterval.None, Now);

        Assert.Throws<InvalidOperationException>(donation.CancelRecurring);
    }

    [Fact]
    public void ResumeRecurring_requires_Paused_state()
    {
        var donation = Donation.Initiate(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 500m, "BDT", isAnonymous: false, RecurrenceInterval.Monthly, Now);

        Assert.Throws<InvalidOperationException>(() => donation.ResumeRecurring(Now));
    }

    [Fact]
    public void ResumeRecurring_after_a_pause_reschedules_NextChargeAt()
    {
        var donation = Donation.Initiate(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 500m, "BDT", isAnonymous: false, RecurrenceInterval.Monthly, Now);
        donation.Fail(Now.AddDays(1));

        donation.ResumeRecurring(Now.AddDays(2));

        Assert.Equal(RecurrenceStatus.Active, donation.RecurrenceStatus);
        Assert.Equal(Now.AddDays(2).AddMonths(1), donation.NextChargeAt);
    }

    [Fact]
    public void CreateNextCycle_copies_donor_campaign_amount_and_links_SeriesRootDonationId()
    {
        var root = Donation.Initiate(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 500m, "BDT", isAnonymous: false, RecurrenceInterval.Monthly, Now);

        var cycle = Donation.CreateNextCycle(root, Now.AddMonths(1));

        Assert.Equal(root.Id, cycle.SeriesRootDonationId);
        Assert.Equal(root.AlumnusId, cycle.AlumnusId);
        Assert.Equal(root.OwnerUserId, cycle.OwnerUserId);
        Assert.Equal(root.Amount, cycle.Amount);
        Assert.False(cycle.IsSeriesRoot);
    }

    [Fact]
    public void CreateNextCycle_on_a_non_root_Donation_throws()
    {
        var root = Donation.Initiate(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 500m, "BDT", isAnonymous: false, RecurrenceInterval.Monthly, Now);
        var cycle = Donation.CreateNextCycle(root, Now.AddMonths(1));

        Assert.Throws<InvalidOperationException>(() => Donation.CreateNextCycle(cycle, Now.AddMonths(2)));
    }
}
