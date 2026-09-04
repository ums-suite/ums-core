using UMS.Modules.Student.Domain.Students;

namespace UMS.Modules.Student.Application.Abstractions;

// Note: the aggregate type is referenced as "Domain.Students.Student" (relative-namespace
// qualification), never bare "Student", throughout the Application/Infrastructure/Api layers -
// the module's own root namespace segment ("UMS.Modules.Student") shares the aggregate's simple
// name, so an unqualified "Student" resolves to the NAMESPACE instead of the type (CS0118)
// anywhere outside the Domain.Students namespace itself.
public interface IStudentRepository
{
    public Task<Domain.Students.Student?> GetByIdAsync(StudentId id, CancellationToken cancellationToken = default);

    public Task<Domain.Students.Student?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    public Task<Domain.Students.Student?> GetByOriginatingApplicationIdAsync(Guid originatingApplicationId, CancellationToken cancellationToken = default);

    public void Add(Domain.Students.Student student);
}
