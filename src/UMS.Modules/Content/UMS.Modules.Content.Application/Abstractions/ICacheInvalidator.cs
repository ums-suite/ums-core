namespace UMS.Modules.Content.Application.Abstractions;

/// <summary>
/// CNT-14/design-decisions.md "Cache-Correctness Backstop": a best-effort, logged-and-continued
/// Redis <c>DEL</c> (or equivalent) against the existing <c>ums-redis</c> instance, called after a
/// Notice/Banner/DownloadResource publish/expire/archive/edit commits. NEVER load-bearing for
/// correctness - a failure here is caught and logged by the implementation, never rethrown, and
/// never rolls back the mutation that already committed. The actual correctness backstop is the
/// origin read path itself (<c>NoticeService.GetByIdAsync</c>'s own Archived check), not this
/// interface - see its own remarks.
/// </summary>
public interface ICacheInvalidator
{
    public Task InvalidateAsync(string cacheKey, CancellationToken cancellationToken = default);
}
