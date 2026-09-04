using Microsoft.EntityFrameworkCore;
using Npgsql;
using UMS.Modules.Documents.Application.Abstractions;
using UMS.Modules.Documents.Domain.Common;
using UMS.Modules.Documents.Domain.GeneratedDocuments;

namespace UMS.Modules.Documents.Infrastructure.Persistence.Repositories;

internal sealed class GeneratedDocumentRepository(DocumentsDbContext context) : IGeneratedDocumentRepository
{
    /// <summary>
    /// Inserts and saves immediately, rather than merely tracking for a later
    /// <c>IUnitOfWork.SaveChangesAsync</c> - edge-cases.md's idempotent-generation race decision
    /// depends on this INSERT round-tripping to Postgres right now, so the natural-key unique
    /// violation (if any) is observable to the caller immediately, not deferred to whenever the
    /// surrounding application service next happens to call <c>SaveChangesAsync</c>.
    /// </summary>
    public async Task AddAsync(GeneratedDocument document, CancellationToken cancellationToken = default)
    {
        context.GeneratedDocuments.Add(document);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task<GeneratedDocument?> GetByIdAsync(GeneratedDocumentId id, CancellationToken cancellationToken = default) =>
        context.GeneratedDocuments.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    public Task<GeneratedDocument?> GetByNaturalKeyAsync(Guid ownerId, DocumentType documentType, Guid sourceReferenceId, CancellationToken cancellationToken = default) =>
        context.GeneratedDocuments.FirstOrDefaultAsync(
            d => d.OwnerId == ownerId && d.DocumentType == documentType && d.SourceReferenceId == sourceReferenceId,
            cancellationToken);

    public Task<GeneratedDocument?> GetByVerificationIdAsync(string digitalVerificationId, CancellationToken cancellationToken = default) =>
        context.GeneratedDocuments.FirstOrDefaultAsync(d => d.DigitalVerificationId == digitalVerificationId, cancellationToken);

    public async Task<IReadOnlyList<GeneratedDocument>> ListAsync(Guid? ownerId, DocumentType? documentType, CancellationToken cancellationToken = default)
    {
        var query = context.GeneratedDocuments.AsQueryable();
        if (ownerId is not null)
        {
            query = query.Where(d => d.OwnerId == ownerId);
        }

        if (documentType is not null)
        {
            query = query.Where(d => d.DocumentType == documentType);
        }

        return await query.OrderByDescending(d => d.CreatedAt).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<GeneratedDocument>> ListStalePendingAsync(DateTimeOffset olderThan, CancellationToken cancellationToken = default) =>
        await context.GeneratedDocuments
            .Where(d => (d.Status == GeneratedDocumentStatus.Pending || d.Status == GeneratedDocumentStatus.Uploaded) && d.CreatedAt < olderThan)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public bool IsNaturalKeyViolation(Exception exception) =>
        exception is DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "ux_generated_documents_natural_key" } };
}
