using UMS.Modules.Learning.Application.Abstractions;

namespace UMS.Modules.Learning.Infrastructure;

/// <summary>The server's own clock - the single authority for <c>Submission.submittedAt</c> (requirement-spec.md learning §4).</summary>
internal sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
