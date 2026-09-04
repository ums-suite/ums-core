namespace UMS.Modules.Documents.Application.BulkJobs;

public sealed record RequestBulkGenerationItemInput(Guid OwnerId, Guid SourceReferenceId, IReadOnlyDictionary<string, string> Fields);
