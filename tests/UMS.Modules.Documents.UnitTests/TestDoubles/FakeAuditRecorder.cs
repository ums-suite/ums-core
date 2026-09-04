using System.Data.Common;
using UMS.Shared.Audit;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Documents.UnitTests.TestDoubles;

public sealed class FakeAuditRecorder : IAuditRecorder
{
    public List<RecordAuditEntryRequest> Recorded { get; } = [];

    public Task<Result> RecordEntryAsync(RecordAuditEntryRequest request, DbTransaction hostTransaction, CancellationToken cancellationToken = default)
    {
        Recorded.Add(request);
        return Task.FromResult(Result.Success());
    }

    public Task<Result> RecordEntriesAsync(IReadOnlyCollection<RecordAuditEntryRequest> requests, DbTransaction hostTransaction, CancellationToken cancellationToken = default)
    {
        Recorded.AddRange(requests);
        return Task.FromResult(Result.Success());
    }
}
