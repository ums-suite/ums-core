namespace UMS.Modules.Alumni.Domain.Donations;

public readonly record struct DonationId(Guid Value)
{
    public static DonationId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
