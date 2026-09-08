namespace UMS.Modules.Alumni.Application.Abstractions;

public interface IClock
{
    public DateTimeOffset UtcNow { get; }
}
