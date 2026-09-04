using UMS.Modules.Documents.Domain.Common;
using UMS.Modules.Documents.Domain.GeneratedDocuments;

namespace UMS.Modules.Documents.Application.Abstractions;

public interface IGeneratedDocumentRepository
{
    /// <summary>Inserts the claim row directly against the database - the caller must catch the natural-key unique-violation itself (edge-cases.md's idempotent-generation race) rather than this method swallowing it, since the correct recovery (fetch-and-wait vs. fail) is an Application-layer decision.</summary>
    public Task AddAsync(GeneratedDocument document, CancellationToken cancellationToken = default);

    public Task<GeneratedDocument?> GetByIdAsync(GeneratedDocumentId id, CancellationToken cancellationToken = default);

    public Task<GeneratedDocument?> GetByNaturalKeyAsync(Guid ownerId, DocumentType documentType, Guid sourceReferenceId, CancellationToken cancellationToken = default);

    public Task<GeneratedDocument?> GetByVerificationIdAsync(string digitalVerificationId, CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<GeneratedDocument>> ListAsync(Guid? ownerId, DocumentType? documentType, CancellationToken cancellationToken = default);

    /// <summary>The compensating-cleanup sweep's own read path (edge-cases.md's object-storage/DB-ordering decision) - rows stuck below <c>Ready</c> past a bounded age.</summary>
    public Task<IReadOnlyList<GeneratedDocument>> ListStalePendingAsync(DateTimeOffset olderThan, CancellationToken cancellationToken = default);

    /// <summary>True if a unique-constraint violation on <c>(OwnerId, DocumentType, SourceReferenceId)</c> is the cause of a just-caught <see cref="Exception"/> from <see cref="AddAsync"/>.</summary>
    public bool IsNaturalKeyViolation(Exception exception);
}
