namespace UMS.Modules.Academic.Application.Abstractions;

public interface IClock
{
    public DateTimeOffset UtcNow { get; }
}
