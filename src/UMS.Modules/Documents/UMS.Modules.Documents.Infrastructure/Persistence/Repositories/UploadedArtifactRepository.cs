using Microsoft.EntityFrameworkCore;
using UMS.Modules.Documents.Application.Abstractions;
using UMS.Modules.Documents.Domain.UploadedArtifacts;

namespace UMS.Modules.Documents.Infrastructure.Persistence.Repositories;

internal sealed class UploadedArtifactRepository(DocumentsDbContext context) : IUploadedArtifactRepository
{
    public void Add(UploadedArtifact artifact) => context.UploadedArtifacts.Add(artifact);

    public Task<UploadedArtifact?> GetByIdAsync(UploadedArtifactId id, CancellationToken cancellationToken = default) =>
        context.UploadedArtifacts.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
}
