namespace UMS.Modules.Audit.Application.Abstractions;

/// <summary>
/// Enqueues an async job trigger into Audit's own <c>audit.outbox_messages</c> table (ADR-0014),
/// in the same transaction as whatever else <see cref="IUnitOfWork.SaveChangesAsync"/> is about to
/// commit - e.g. AUD-9's <c>POST /audit/exports</c> creates the <c>AuditExportRequest</c> row and
/// enqueues the "go generate this export" trigger atomically, so a request can never be accepted
/// with no worker ever picking it up.
/// </summary>
public interface IOutboxEnqueuer
{
    public void Enqueue(string eventType, string payloadJson, DateTimeOffset occurredAt);
}
