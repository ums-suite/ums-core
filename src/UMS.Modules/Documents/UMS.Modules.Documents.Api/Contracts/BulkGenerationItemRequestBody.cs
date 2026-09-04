namespace UMS.Modules.Documents.Api.Contracts;

public sealed record BulkGenerationItemRequestBody(Guid OwnerId, Guid SourceReferenceId, IReadOnlyDictionary<string, string> Fields);
