namespace UMS.Modules.Admission.Domain.Campaigns;

public readonly record struct AdmissionCampaignId(Guid Value)
{
    public static AdmissionCampaignId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
