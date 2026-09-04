namespace UMS.Modules.Documents.Application.BulkJobs;

/// <summary>The outbox payload the bulk-generation relay worker deserializes - the worker re-reads the job's own pinned template/items rather than trusting a duplicated copy, mirroring Audit's own <c>AuditExportRequestedPayload</c> convention.</summary>
public sealed record BulkGenerationJobRequestedPayload(Guid JobId);
