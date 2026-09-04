using Microsoft.EntityFrameworkCore;
using UMS.Modules.Documents.Application.Abstractions;
using UMS.Modules.Documents.Domain.Common;
using UMS.Modules.Documents.Domain.Templates;

namespace UMS.Modules.Documents.Infrastructure.Persistence.Repositories;

internal sealed class DocumentTemplateRepository(DocumentsDbContext context) : IDocumentTemplateRepository
{
    public void Add(DocumentTemplate template) => context.Templates.Add(template);

    public Task<DocumentTemplate?> GetByIdAsync(DocumentTemplateId id, CancellationToken cancellationToken = default) =>
        context.Templates.Include(t => t.Translations).FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public Task<DocumentTemplate?> GetCurrentAsync(DocumentType documentType, CancellationToken cancellationToken = default) =>
        context.Templates
            .Include(t => t.Translations)
            .Where(t => t.DocumentType == documentType)
            .OrderByDescending(t => t.Version)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<int> GetLatestVersionAsync(DocumentType documentType, CancellationToken cancellationToken = default)
    {
        var latest = await context.Templates
            .Where(t => t.DocumentType == documentType)
            .OrderByDescending(t => t.Version)
            .Select(t => (int?)t.Version)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return latest ?? 0;
    }

    public async Task<IReadOnlyList<DocumentTemplate>> ListAsync(DocumentType? documentType, CancellationToken cancellationToken = default)
    {
        var query = context.Templates.Include(t => t.Translations).AsQueryable();
        if (documentType is not null)
        {
            query = query.Where(t => t.DocumentType == documentType);
        }

        return await query.OrderBy(t => t.DocumentType).ThenByDescending(t => t.Version).ToListAsync(cancellationToken).ConfigureAwait(false);
    }
}
