namespace UMS.Modules.Research.Application.Abstractions;

public interface IClock
{
    public DateTimeOffset UtcNow { get; }
}
