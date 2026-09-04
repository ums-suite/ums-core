using UMS.Modules.Documents.Domain.Common;

namespace UMS.Modules.Documents.Application.BulkJobs;

public sealed record RequestBulkGenerationCommand(DocumentType DocumentType, Guid RequestedByUserId, IReadOnlyCollection<RequestBulkGenerationItemInput> Items);
