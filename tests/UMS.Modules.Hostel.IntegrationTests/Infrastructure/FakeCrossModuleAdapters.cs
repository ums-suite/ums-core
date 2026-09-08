using System.Collections.Concurrent;
using UMS.Shared.ErrorHandling.Results;
using UMS.Shared.Finance;
using UMS.Shared.Notifications;
using UMS.Shared.Student;

namespace UMS.Modules.Hostel.IntegrationTests.Infrastructure;

/// <summary>
/// This suite tests Hostel's OWN concurrency/state-machine invariants against a real Postgres
/// (Testcontainers) - Finance/Notifications/Student are genuinely different modules with their own
/// already-verified test suites, so faking their cross-module contracts here keeps this suite's own
/// fixture focused on what it actually exercises, mirroring Admission's own <c>FakeCrossModuleAdapters</c>
/// posture exactly.
/// </summary>
internal sealed class FakeInvoiceRequester : IInvoiceRequester
{
    public Task<Result<InvoiceSummary>> CreateInvoiceAsync(CreateInvoiceCommand command, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Success(new InvoiceSummary(Guid.NewGuid(), "Open", 5000m, "BDT")));
}

internal sealed class FakeNotificationRequestIntake : INotificationRequestIntake
{
    public Task<Result<Guid>> SubmitAsync(SubmitNotificationRequestCommand request, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Success(Guid.NewGuid()));
}

/// <summary>Registers a per-Student <see cref="StudentAcademicStanding"/> for tests that need a specific Program/Status; any unregistered Student resolves to a generic "Active" standing so happy-path tests don't need explicit setup.</summary>
public sealed class FakeStudentStatusChecker : IStudentStatusChecker
{
    private readonly ConcurrentDictionary<Guid, StudentAcademicStanding> _byStudentId = new();

    public void Register(StudentAcademicStanding standing) => _byStudentId[standing.StudentId] = standing;

    public Task<StudentAcademicStanding?> GetByStudentIdAsync(Guid studentId, CancellationToken cancellationToken = default) =>
        Task.FromResult<StudentAcademicStanding?>(_byStudentId.TryGetValue(studentId, out var standing)
            ? standing
            : new StudentAcademicStanding(studentId, Guid.NewGuid(), Guid.NewGuid(), "Active", studentId));

    public Task<StudentAcademicStanding?> GetByUserIdAsync(Guid identityUserId, CancellationToken cancellationToken = default) =>
        Task.FromResult<StudentAcademicStanding?>(_byStudentId.Values.FirstOrDefault(s => s.IdentityUserId == identityUserId)
            ?? new StudentAcademicStanding(identityUserId, Guid.NewGuid(), Guid.NewGuid(), "Active", identityUserId));
}
