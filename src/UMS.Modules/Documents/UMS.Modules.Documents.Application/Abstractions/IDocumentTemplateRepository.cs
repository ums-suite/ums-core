using UMS.Modules.Documents.Domain.Common;
using UMS.Modules.Documents.Domain.Templates;

namespace UMS.Modules.Documents.Application.Abstractions;

public interface IDocumentTemplateRepository
{
    public void Add(DocumentTemplate template);

    public Task<DocumentTemplate?> GetByIdAsync(DocumentTemplateId id, CancellationToken cancellationToken = default);

    /// <summary>The highest-<c>Version</c> template for a type - "the current template" resolved server-side, never client-supplied (requirement-spec.md documents §2).</summary>
    public Task<DocumentTemplate?> GetCurrentAsync(DocumentType documentType, CancellationToken cancellationToken = default);

    public Task<int> GetLatestVersionAsync(DocumentType documentType, CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<DocumentTemplate>> ListAsync(DocumentType? documentType, CancellationToken cancellationToken = default);
}
