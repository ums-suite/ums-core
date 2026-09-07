using Microsoft.EntityFrameworkCore;
using UMS.Modules.Academic.Application.Abstractions;
using UMS.Modules.Academic.Domain.Curricula;

namespace UMS.Modules.Academic.Infrastructure.Persistence.Repositories;

internal sealed class CurriculumRepository(AcademicDbContext context) : ICurriculumRepository
{
    public Task<Curriculum?> GetByIdAsync(CurriculumId id, CancellationToken cancellationToken = default) =>
        context.Curricula.Include(c => c.Courses).FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public void Add(Curriculum curriculum) => context.Curricula.Add(curriculum);
}
