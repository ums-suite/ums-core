using Microsoft.EntityFrameworkCore;
using UMS.Modules.Student.Application.Abstractions;
using UMS.Modules.Student.Domain.StudentRequests;

namespace UMS.Modules.Student.Infrastructure.Persistence.Repositories;

internal sealed class StudentRequestRepository(StudentDbContext context) : IStudentRequestRepository
{
    public Task<StudentRequest?> GetByIdAsync(StudentRequestId id, CancellationToken cancellationToken = default) =>
        context.StudentRequests.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public Task<StudentRequest?> GetOpenByStudentAndTypeAsync(Guid studentId, StudentRequestType requestType, CancellationToken cancellationToken = default) =>
        context.StudentRequests.FirstOrDefaultAsync(
            r => r.StudentId == studentId && r.RequestType == requestType && (r.Status == StudentRequestStatus.Submitted || r.Status == StudentRequestStatus.UnderReview),
            cancellationToken);

    public void Add(StudentRequest request) => context.StudentRequests.Add(request);
}
