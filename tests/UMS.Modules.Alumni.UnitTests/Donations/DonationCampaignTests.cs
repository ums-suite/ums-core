using UMS.Modules.Alumni.Domain.Donations;

namespace UMS.Modules.Alumni.UnitTests.Donations;

/// <summary>design-decisions.md "Donation-vs-Campaign-Close Consistency Boundary": IsActive is the ONLY gate, checked exclusively at Donation-creation time by the calling service.</summary>
public sealed class DonationCampaignTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void IsActive_true_within_the_window()
    {
        var campaign = DonationCampaign.Create("Scholarship Fund", null, 100_000m, "BDT", Now.AddDays(-1), Now.AddDays(30), Now);

        Assert.True(campaign.IsActive(Now));
    }

    [Fact]
    public void IsActive_false_before_the_window_starts()
    {
        var campaign = DonationCampaign.Create("Scholarship Fund", null, 100_000m, "BDT", Now.AddDays(1), Now.AddDays(30), Now);

        Assert.False(campaign.IsActive(Now));
    }

    [Fact]
    public void IsActive_false_after_the_window_ends()
    {
        var campaign = DonationCampaign.Create("Scholarship Fund", null, 100_000m, "BDT", Now.AddDays(-30), Now.AddDays(-1), Now);

        Assert.False(campaign.IsActive(Now));
    }

    [Fact]
    public void CloseEarly_deactivates_even_within_the_original_window()
    {
        var campaign = DonationCampaign.Create("Scholarship Fund", null, 100_000m, "BDT", Now.AddDays(-1), Now.AddDays(30), Now);

        campaign.CloseEarly();

        Assert.False(campaign.IsActive(Now));
    }
}
