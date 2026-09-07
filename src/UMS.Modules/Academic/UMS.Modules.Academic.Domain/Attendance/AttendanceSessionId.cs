namespace UMS.Modules.Academic.Domain.Attendance;

public readonly record struct AttendanceSessionId(Guid Value)
{
    public static AttendanceSessionId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
