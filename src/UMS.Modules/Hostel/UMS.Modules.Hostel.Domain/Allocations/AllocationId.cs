namespace UMS.Modules.Hostel.Domain.Allocations;

public readonly record struct AllocationId(Guid Value)
{
    public static AllocationId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
