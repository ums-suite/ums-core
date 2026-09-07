using UMS.Modules.Admission.Domain.Campaigns;
using UMS.Shared.Domain;

namespace UMS.Modules.Admission.UnitTests.Campaigns;

/// <summary>ADM-1: requirement-spec.md §2 Campaign Setup, §9 decision 6's configuration-immutability rule.</summary>
public sealed class AdmissionCampaignTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static AdmissionCampaign CreateCampaign(Guid programId)
    {
        var window = DateRange.Create(new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 1)).Value;
        return AdmissionCampaign.Create("Spring 2026", [programId], window, "ApplicationFee", "ConfirmationFee", Now).Value;
    }

    [Fact]
    public void A_campaign_with_no_programs_is_rejected()
    {
        var window = DateRange.Create(new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 1)).Value;
        var result = AdmissionCampaign.Create("Spring 2026", [], window, "ApplicationFee", "ConfirmationFee", Now);

        Assert.True(result.IsFailure);
        Assert.Equal("campaign.programs_required", result.Error!.Code);
    }

    [Fact]
    public void Adding_an_eligibility_rule_for_a_program_outside_the_campaign_is_rejected()
    {
        var campaign = CreateCampaign(Guid.NewGuid());
        var rule = EligibilityRule.Create(Guid.NewGuid(), PercentageOrGpa.CreatePercentage(60).Value, requiredBoard: null).Value;

        var result = campaign.AddEligibilityRule(rule);

        Assert.True(result.IsFailure);
        Assert.Equal("campaign.rule_program_not_in_campaign", result.Error!.Code);
    }

    [Fact]
    public void Configuration_edits_are_rejected_once_locked()
    {
        var programId = Guid.NewGuid();
        var campaign = CreateCampaign(programId);
        campaign.LockConfiguration();

        var quota = SeatQuota.Create(programId, 10).Value;
        var result = campaign.AddSeatQuota(quota);

        Assert.True(result.IsFailure);
        Assert.Equal("campaign.configuration_locked", result.Error!.Code);
    }

    [Fact]
    public void Locking_configuration_twice_is_a_harmless_no_op()
    {
        var campaign = CreateCampaign(Guid.NewGuid());
        campaign.LockConfiguration();
        campaign.LockConfiguration();

        Assert.True(campaign.IsConfigurationLocked);
    }

    [Fact]
    public void An_eligibility_rule_is_satisfied_only_within_the_same_score_scale()
    {
        var programId = Guid.NewGuid();
        var campaign = CreateCampaign(programId);
        var rule = EligibilityRule.Create(programId, PercentageOrGpa.CreatePercentage(60).Value, requiredBoard: null).Value;
        campaign.AddEligibilityRule(rule);

        var gpaRecord = Domain.Applicants.AcademicRecord.Create("Dhaka Board", "HSC", 2024, PercentageOrGpa.CreateGpa(3.8m).Value).Value;
        var percentageRecord = Domain.Applicants.AcademicRecord.Create("Dhaka Board", "HSC", 2024, PercentageOrGpa.CreatePercentage(75).Value).Value;

        Assert.False(rule.IsSatisfiedBy(gpaRecord));
        Assert.True(rule.IsSatisfiedBy(percentageRecord));
    }
}
