namespace UMS.Modules.Faculty.Domain.LeaveRequests;

public readonly record struct LeaveRequestId(Guid Value)
{
    public static LeaveRequestId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
