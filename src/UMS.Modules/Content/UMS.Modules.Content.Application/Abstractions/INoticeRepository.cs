using UMS.Modules.Content.Domain.Common;
using UMS.Modules.Content.Domain.Notices;

namespace UMS.Modules.Content.Application.Abstractions;

public interface INoticeRepository
{
    public Task<Notice?> GetByIdAsync(NoticeId id, CancellationToken cancellationToken = default);

    public void Add(Notice notice);

    /// <summary>Admin/list-management read - every status, optionally filtered.</summary>
    public Task<IReadOnlyList<Notice>> ListAsync(ContentAudience? audience, SchedulableStatus? status, int skip, int take, CancellationToken cancellationToken = default);

    public Task<int> CountAsync(ContentAudience? audience, SchedulableStatus? status, CancellationToken cancellationToken = default);

    /// <summary>CNT-4: `publish_at &lt;= now AND status = Scheduled` - the job's own idempotent scan (design-decisions.md "Scheduled-Publish Job Exactly-Once Execution Mechanism").</summary>
    public Task<IReadOnlyList<Notice>> GetDueForPublishAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken = default);

    /// <summary>`expire_at &lt;= now AND status = Published`.</summary>
    public Task<IReadOnlyList<Notice>> GetDueForExpireAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken = default);
}
