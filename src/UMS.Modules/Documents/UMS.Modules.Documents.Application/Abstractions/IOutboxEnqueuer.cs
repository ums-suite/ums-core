namespace UMS.Modules.Documents.Application.Abstractions;

/// <summary>Enqueues an async job trigger into Documents' own <c>documents.outbox_messages</c> table (ADR-0014), in the same transaction as whatever else <see cref="IUnitOfWork.SaveChangesAsync"/> is about to commit - mirrors Audit's own <c>IOutboxEnqueuer</c>.</summary>
public interface IOutboxEnqueuer
{
    public void Enqueue(string eventType, string payloadJson, DateTimeOffset occurredAt);
}
