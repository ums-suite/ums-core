using UMS.Modules.Documents.Domain.UploadedArtifacts;

namespace UMS.Modules.Documents.Application.Abstractions;

public interface IUploadedArtifactRepository
{
    public void Add(UploadedArtifact artifact);

    public Task<UploadedArtifact?> GetByIdAsync(UploadedArtifactId id, CancellationToken cancellationToken = default);
}
