namespace UMS.Modules.Organization.Application.Abstractions;

public interface IClock
{
    public DateTimeOffset UtcNow { get; }
}
