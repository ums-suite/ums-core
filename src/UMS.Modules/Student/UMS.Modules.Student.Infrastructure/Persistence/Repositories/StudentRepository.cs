using Microsoft.EntityFrameworkCore;
using UMS.Modules.Student.Application.Abstractions;
using UMS.Modules.Student.Domain.Students;

namespace UMS.Modules.Student.Infrastructure.Persistence.Repositories;

internal sealed class StudentRepository(StudentDbContext context) : IStudentRepository
{
    public Task<Domain.Students.Student?> GetByIdAsync(StudentId id, CancellationToken cancellationToken = default) =>
        context.Students
            .Include(s => s.StatusHistory)
            .Include(s => s.Guardians)
            .Include(s => s.GuardianAccessGrants)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public Task<Domain.Students.Student?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        context.Students
            .Include(s => s.StatusHistory)
            .Include(s => s.Guardians)
            .Include(s => s.GuardianAccessGrants)
            .FirstOrDefaultAsync(s => s.IdentityUserId == userId, cancellationToken);

    public Task<Domain.Students.Student?> GetByOriginatingApplicationIdAsync(Guid originatingApplicationId, CancellationToken cancellationToken = default) =>
        context.Students.FirstOrDefaultAsync(s => s.OriginatingApplicationId == originatingApplicationId, cancellationToken);

    public Task<Domain.Students.Student?> GetByStudentNumberAsync(string studentNumber, CancellationToken cancellationToken = default) =>
        context.Students.FirstOrDefaultAsync(s => s.StudentNumber.Value == studentNumber, cancellationToken);

    public void Add(Domain.Students.Student student) => context.Students.Add(student);
}
