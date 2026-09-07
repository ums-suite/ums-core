using Microsoft.EntityFrameworkCore;
using UMS.Modules.Learning.Application.Abstractions;
using UMS.Modules.Learning.Domain.LectureMaterials;

namespace UMS.Modules.Learning.Infrastructure.Persistence.Repositories;

internal sealed class LectureMaterialRepository(LearningDbContext context) : ILectureMaterialRepository
{
    public Task<LectureMaterial?> GetByIdAsync(LectureMaterialId id, CancellationToken cancellationToken = default) =>
        Query().FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

    /// <summary>LRN-15: the syllabus-like ordering requirement-spec.md §2 describes - module/week grouping first, explicit sort order within it.</summary>
    public async Task<IReadOnlyList<LectureMaterial>> GetByCourseOfferingAsync(Guid courseOfferingId, CancellationToken cancellationToken = default) =>
        await Query()
            .Where(m => m.CourseOfferingId == courseOfferingId)
            .OrderBy(m => m.ModuleGroup)
            .ThenBy(m => m.SortOrder)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public void Add(LectureMaterial material) => context.LectureMaterials.Add(material);

    private IQueryable<LectureMaterial> Query() =>
        context.LectureMaterials.Include(m => m.Versions).Include(m => m.Translations);
}
