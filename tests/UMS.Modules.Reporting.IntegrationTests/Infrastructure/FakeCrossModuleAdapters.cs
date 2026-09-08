using System.Data.Common;
using UMS.Shared.Audit;
using UMS.Shared.Documents;
using UMS.Shared.ErrorHandling.Results;
using UMS.Shared.Notifications;

namespace UMS.Modules.Reporting.IntegrationTests.Infrastructure;

/// <summary>Audit/Documents/Notifications are genuinely different, already-verified modules - see <c>FakeAcademicReportingQuery</c>'s own remarks.</summary>
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

public sealed class FakeDocumentGenerationRequester : IDocumentGenerationRequester
{
    public Result<GeneratedDocumentSummary> NextResult { get; set; } = new GeneratedDocumentSummary(Guid.NewGuid(), "Ready", "https://fake-documents.internal/doc.pdf");

    public RequestDocumentGenerationCommand? LastCommand { get; private set; }

    public Task<Result<GeneratedDocumentSummary>> RequestAsync(RequestDocumentGenerationCommand command, CancellationToken cancellationToken = default)
    {
        LastCommand = command;
        return Task.FromResult(NextResult);
    }
}

public sealed class FakeNotificationRequestIntake : INotificationRequestIntake
{
    public List<SubmitNotificationRequestCommand> Submitted { get; } = [];

    public Task<Result<Guid>> SubmitAsync(SubmitNotificationRequestCommand request, CancellationToken cancellationToken = default)
    {
        Submitted.Add(request);
        return Task.FromResult(Result.Success(Guid.NewGuid()));
    }
}
