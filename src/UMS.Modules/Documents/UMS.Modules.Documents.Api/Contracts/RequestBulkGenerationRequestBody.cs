namespace UMS.Modules.Documents.Api.Contracts;

public sealed record RequestBulkGenerationRequestBody(string DocumentType, IReadOnlyCollection<BulkGenerationItemRequestBody> Items);
