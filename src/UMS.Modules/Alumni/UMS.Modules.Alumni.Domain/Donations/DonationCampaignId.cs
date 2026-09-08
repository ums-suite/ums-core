namespace UMS.Modules.Alumni.Domain.Donations;

public readonly record struct DonationCampaignId(Guid Value)
{
    public static DonationCampaignId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
