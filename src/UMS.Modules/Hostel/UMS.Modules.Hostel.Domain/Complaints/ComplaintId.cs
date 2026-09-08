namespace UMS.Modules.Hostel.Domain.Complaints;

public readonly record struct ComplaintId(Guid Value)
{
    public static ComplaintId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
