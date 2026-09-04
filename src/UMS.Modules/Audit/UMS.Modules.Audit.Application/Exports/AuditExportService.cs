using System.Text.Json;
using UMS.Modules.Audit.Application.Abstractions;
using UMS.Modules.Audit.Domain.Exports;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Audit.Application.Exports;

/// <summary>
/// AUD-9/AUD-10: creates an async export job and reports its status. The actual export
/// generation (querying Audit's read side, rendering CSV/PDF, uploading to object storage) runs
/// out-of-request, in <c>UMS.Workers</c>, off the outbox message enqueued here (ADR-0014) - this
/// service never touches the filtered result set itself, keeping the synchronous request path
/// (and Audit's own p95 write-latency budget, which every OTHER module's write shares) untouched
/// by a potentially large export query.
/// </summary>
public sealed class AuditExportService(
    IAuditExportRequestRepository exportRequests,
    IUnitOfWork unitOfWork,
    IOutboxEnqueuer outbox,
    IObjectStorage objectStorage,
    IClock clock)
{
    private const string ExportRequestedEventType = "AuditExportRequested";

    public async Task<AuditExportRequestDto> RequestExportAsync(Guid requestedByUserId, RequestExportCommand command, CancellationToken cancellationToken = default)
    {
        var filterJson = JsonSerializer.Serialize(command.Filter);
        var now = clock.UtcNow;
        var request = AuditExportRequest.Create(requestedByUserId, filterJson, command.Format, now);

        exportRequests.Add(request);
        outbox.Enqueue(ExportRequestedEventType, JsonSerializer.Serialize(new AuditExportRequestedPayload(request.Id)), now);

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return AuditExportRequestDto.FromDomain(request, downloadUrl: null);
    }

    public async Task<Result<AuditExportRequestDto>> GetStatusAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var request = await exportRequests.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (request is null)
        {
            return Error.NotFound("audit_export.not_found", $"No export request exists with id '{id}'.");
        }

        string? downloadUrl = null;
        if (request.Status == ExportStatus.Completed && request.ResultObjectKey is not null)
        {
            downloadUrl = await objectStorage.GetDownloadUrlAsync(request.ResultObjectKey, TimeSpan.FromMinutes(15), cancellationToken).ConfigureAwait(false);
        }

        return AuditExportRequestDto.FromDomain(request, downloadUrl);
    }
}

/// <summary>The outbox payload shape <c>UMS.Workers</c>' export relay deserializes - kept to just the id, since the worker re-reads the authoritative filter/format from the <see cref="AuditExportRequest"/> row itself rather than trusting a duplicated copy in the outbox payload.</summary>
public sealed record AuditExportRequestedPayload(Guid ExportRequestId);
