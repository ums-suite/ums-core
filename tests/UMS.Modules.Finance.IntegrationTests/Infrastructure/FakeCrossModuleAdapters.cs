using System.Data.Common;
using UMS.Shared.Audit;
using UMS.Shared.Documents;
using UMS.Shared.ErrorHandling.Results;
using UMS.Shared.Notifications;

namespace UMS.Modules.Finance.IntegrationTests.Infrastructure;

/// <summary>
/// This suite tests Finance's OWN concurrency/state-machine invariants against a real Postgres
/// (Testcontainers) - Audit/Documents/Notifications are genuinely different modules with their own
/// already-verified test suites, so faking their cross-module contracts here (rather than pulling in
/// their entire Infrastructure/Testcontainers stack too) keeps this suite's own fixture focused on
/// what it actually exercises, the same posture Learning's own fixture takes toward modules it
/// merely depends on rather than tests.
/// </summary>
internal sealed class FakeAuditRecorder : IAuditRecorder
{
    public Task<Result> RecordEntryAsync(RecordAuditEntryRequest request, DbTransaction hostTransaction, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Success());

    public Task<Result> RecordEntriesAsync(IReadOnlyCollection<RecordAuditEntryRequest> requests, DbTransaction hostTransaction, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Success());
}

internal sealed class FakeDocumentGenerationRequester : IDocumentGenerationRequester
{
    public Task<Result<GeneratedDocumentSummary>> RequestAsync(RequestDocumentGenerationCommand command, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Success(new GeneratedDocumentSummary(Guid.NewGuid(), "Ready", "https://fake-documents.internal/receipt.pdf")));
}

internal sealed class FakeNotificationRequestIntake : INotificationRequestIntake
{
    public Task<Result<Guid>> SubmitAsync(SubmitNotificationRequestCommand request, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Success(Guid.NewGuid()));
}
