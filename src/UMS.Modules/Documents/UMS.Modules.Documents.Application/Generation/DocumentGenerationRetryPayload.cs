namespace UMS.Modules.Documents.Application.Generation;

/// <summary>DOC-15: the outbox payload enqueued when a synchronous render's object-storage upload fails (edge-cases.md's object-storage-outage-during-sync-receipt edge case) - the worker re-reads the claim row's own <c>RenderDataJson</c>/<c>Language</c> rather than trusting a duplicated copy here, mirroring Audit's own <c>AuditExportRequestedPayload</c> convention.</summary>
public sealed record DocumentGenerationRetryPayload(Guid GeneratedDocumentId);
