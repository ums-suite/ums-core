using UMS.Modules.Documents.Application.Abstractions;
using UMS.Modules.Documents.Domain.Common;
using UMS.Modules.Documents.Domain.Templates;

namespace UMS.Modules.Documents.UnitTests.TestDoubles;

public sealed class FakeDocumentTemplateRepository : IDocumentTemplateRepository
{
    private readonly List<DocumentTemplate> _templates = [];

    public void Add(DocumentTemplate template) => _templates.Add(template);

    public Task<DocumentTemplate?> GetByIdAsync(DocumentTemplateId id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_templates.FirstOrDefault(t => t.Id == id));

    public Task<DocumentTemplate?> GetCurrentAsync(DocumentType documentType, CancellationToken cancellationToken = default) =>
        Task.FromResult(_templates.Where(t => t.DocumentType == documentType).OrderByDescending(t => t.Version).FirstOrDefault());

    public Task<int> GetLatestVersionAsync(DocumentType documentType, CancellationToken cancellationToken = default) =>
        Task.FromResult(_templates.Where(t => t.DocumentType == documentType).Select(t => t.Version).DefaultIfEmpty(0).Max());

    public Task<IReadOnlyList<DocumentTemplate>> ListAsync(DocumentType? documentType, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<DocumentTemplate>>(_templates.Where(t => documentType == null || t.DocumentType == documentType).ToList());
}
