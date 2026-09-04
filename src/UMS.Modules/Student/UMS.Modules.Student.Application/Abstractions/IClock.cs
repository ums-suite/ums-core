namespace UMS.Modules.Student.Application.Abstractions;

public interface IClock
{
    public DateTimeOffset UtcNow { get; }
}
