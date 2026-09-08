namespace UMS.Modules.Career.Application.Abstractions;

public interface IClock
{
    public DateTimeOffset UtcNow { get; }
}
