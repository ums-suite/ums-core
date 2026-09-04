namespace UMS.Modules.Faculty.Application.Abstractions;

public interface IClock
{
    public DateTimeOffset UtcNow { get; }
}
