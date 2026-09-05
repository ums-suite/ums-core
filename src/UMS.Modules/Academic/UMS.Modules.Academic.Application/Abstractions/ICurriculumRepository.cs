using UMS.Modules.Academic.Domain.Curricula;

namespace UMS.Modules.Academic.Application.Abstractions;

public interface ICurriculumRepository
{
    public Task<Curriculum?> GetByIdAsync(CurriculumId id, CancellationToken cancellationToken = default);

    public void Add(Curriculum curriculum);
}
