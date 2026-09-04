using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Audit.Domain.Exports;

/// <summary>
/// AUD-9/AUD-10: tracks one async, filtered audit export job, delivered via the shared
/// outbox/worker pattern (ADR-0014). This is a supporting job-tracking entity for the export
/// *feature*, not audited business data itself - requirement-spec.md §1's "Audit owns exactly one
/// aggregate, AuditLogEntry" refers to the append-only ledger of *other modules'* sensitive
/// mutations; an export job about Audit's own read path has no bearing on that invariant, the same
/// way <c>UMS.Shared.Outbox.OutboxMessage</c> is plumbing rather than a domain aggregate. It still
/// gets a real state machine (never an anemic property bag, ums-conventions.md) because
/// "Completed job silently reset to Processing" is exactly the kind of bug a rich model prevents.
/// </summary>
public sealed class AuditExportRequest
{
    private AuditExportRequest()
    {
    }

    public Guid Id { get; private init; }

    public Guid RequestedByUserId { get; private init; }

    public string FilterJson { get; private init; } = string.Empty;

    public ExportFormat Format { get; private init; }

    public ExportStatus Status { get; private set; }

    public DateTimeOffset RequestedAt { get; private init; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public string? ResultObjectKey { get; private set; }

    public string? ErrorMessage { get; private set; }

    public static AuditExportRequest Create(Guid requestedByUserId, string filterJson, ExportFormat format, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        RequestedByUserId = requestedByUserId,
        FilterJson = filterJson,
        Format = format,
        Status = ExportStatus.Pending,
        RequestedAt = now,
    };

    public Result MarkProcessing()
    {
        if (Status != ExportStatus.Pending)
        {
            return Result.Failure(Error.Conflict("audit_export.invalid_transition", $"Cannot start processing an export request in status '{Status}'."));
        }

        Status = ExportStatus.Processing;
        return Result.Success();
    }

    public Result MarkCompleted(string resultObjectKey, DateTimeOffset now)
    {
        if (Status != ExportStatus.Processing)
        {
            return Result.Failure(Error.Conflict("audit_export.invalid_transition", $"Cannot complete an export request in status '{Status}'."));
        }

        if (string.IsNullOrWhiteSpace(resultObjectKey))
        {
            return Result.Failure(Error.Validation("audit_export.result_key_required", "A completed export must reference its result object key."));
        }

        Status = ExportStatus.Completed;
        ResultObjectKey = resultObjectKey;
        CompletedAt = now;
        return Result.Success();
    }

    public Result MarkFailed(string errorMessage, DateTimeOffset now)
    {
        if (Status is ExportStatus.Completed or ExportStatus.Failed)
        {
            return Result.Failure(Error.Conflict("audit_export.invalid_transition", $"Cannot fail an export request already in a terminal status '{Status}'."));
        }

        Status = ExportStatus.Failed;
        ErrorMessage = errorMessage;
        CompletedAt = now;
        return Result.Success();
    }
}
