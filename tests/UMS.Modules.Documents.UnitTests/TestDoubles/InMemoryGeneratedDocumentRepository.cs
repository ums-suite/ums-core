using UMS.Modules.Documents.Application.Abstractions;
using UMS.Modules.Documents.Domain.Common;
using UMS.Modules.Documents.Domain.GeneratedDocuments;

namespace UMS.Modules.Documents.UnitTests.TestDoubles;

/// <summary>
/// An in-memory stand-in for <see cref="IGeneratedDocumentRepository"/> that enforces the same
/// <c>(OwnerId, DocumentType, SourceReferenceId)</c> natural-key uniqueness a real Postgres unique
/// index would (edge-cases.md's idempotent-generation race decision) - lets
/// <see cref="Application.Generation.GenerateDocumentService"/>'s branch logic be exercised without
/// Testcontainers. True concurrent-request timing is still verified only at the integration-test
/// level (mirrors Organization's own "sequential-outcome equivalent" framing for this class of race).
/// </summary>
public sealed class InMemoryGeneratedDocumentRepository : IGeneratedDocumentRepository
{
    private readonly List<GeneratedDocument> _documents = [];

    public Task AddAsync(GeneratedDocument document, CancellationToken cancellationToken = default)
    {
        if (_documents.Any(d => d.OwnerId == document.OwnerId && d.DocumentType == document.DocumentType && d.SourceReferenceId == document.SourceReferenceId))
        {
            throw new NaturalKeyViolationException();
        }

        _documents.Add(document);
        return Task.CompletedTask;
    }

    public Task<GeneratedDocument?> GetByIdAsync(GeneratedDocumentId id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_documents.FirstOrDefault(d => d.Id == id));

    public Task<GeneratedDocument?> GetByNaturalKeyAsync(Guid ownerId, DocumentType documentType, Guid sourceReferenceId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_documents.FirstOrDefault(d => d.OwnerId == ownerId && d.DocumentType == documentType && d.SourceReferenceId == sourceReferenceId));

    public Task<GeneratedDocument?> GetByVerificationIdAsync(string digitalVerificationId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_documents.FirstOrDefault(d => d.DigitalVerificationId == digitalVerificationId));

    public Task<IReadOnlyList<GeneratedDocument>> ListAsync(Guid? ownerId, DocumentType? documentType, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<GeneratedDocument>>(_documents
            .Where(d => (ownerId == null || d.OwnerId == ownerId) && (documentType == null || d.DocumentType == documentType))
            .ToList());

    public Task<IReadOnlyList<GeneratedDocument>> ListStalePendingAsync(DateTimeOffset olderThan, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<GeneratedDocument>>(_documents
            .Where(d => d.Status is GeneratedDocumentStatus.Pending or GeneratedDocumentStatus.Uploaded && d.CreatedAt < olderThan)
            .ToList());

    public bool IsNaturalKeyViolation(Exception exception) => exception is NaturalKeyViolationException;
}

public sealed class NaturalKeyViolationException : Exception;
