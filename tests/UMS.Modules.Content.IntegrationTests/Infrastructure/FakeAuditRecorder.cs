using System.Data.Common;
using UMS.Shared.Audit;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Content.IntegrationTests.Infrastructure;

/// <summary>Audit is a genuinely different, already-verified module - see <c>FakeCrossModuleAdapters</c>'s own remarks. Mirrors every other module's own fake exactly.</summary>
internal sealed class FakeAuditRecorder : IAuditRecorder
{
    public Task<Result> RecordEntryAsync(RecordAuditEntryRequest request, DbTransaction hostTransaction, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Success());

    public Task<Result> RecordEntriesAsync(IReadOnlyCollection<RecordAuditEntryRequest> requests, DbTransaction hostTransaction, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Success());
}
