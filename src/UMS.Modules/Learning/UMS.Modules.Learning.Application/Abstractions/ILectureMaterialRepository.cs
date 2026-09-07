using UMS.Modules.Learning.Domain.LectureMaterials;

namespace UMS.Modules.Learning.Application.Abstractions;

public interface ILectureMaterialRepository
{
    public Task<LectureMaterial?> GetByIdAsync(LectureMaterialId id, CancellationToken cancellationToken = default);

    /// <summary>LRN-15: ordered by <c>(moduleGroup, sortOrder)</c> - the syllabus-like structure requirement-spec.md §2 describes.</summary>
    public Task<IReadOnlyList<LectureMaterial>> GetByCourseOfferingAsync(Guid courseOfferingId, CancellationToken cancellationToken = default);

    public void Add(LectureMaterial material);
}
