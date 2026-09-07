using UMS.Modules.Student.Domain.StudentRequests;

namespace UMS.Modules.Student.Application.Abstractions;

public interface IStudentRequestRepository
{
    public Task<StudentRequest?> GetByIdAsync(StudentRequestId id, CancellationToken cancellationToken = default);

    /// <summary>design-decisions.md "StudentRequest Dedup Mechanism": the fast-path, user-friendly pre-check ONLY - the partial unique index (<c>StudentRequestConfiguration</c>) is the real enforcement, translated from a caught constraint violation in <c>StudentDbContext</c>.</summary>
    public Task<StudentRequest?> GetOpenByStudentAndTypeAsync(Guid studentId, StudentRequestType requestType, CancellationToken cancellationToken = default);

    public void Add(StudentRequest request);
}
