using System.Collections.Concurrent;
using UMS.Shared.ErrorHandling.Results;
using UMS.Shared.Faculty;
using UMS.Shared.Finance;
using UMS.Shared.Notifications;
using UMS.Shared.Student;

namespace UMS.Modules.Library.IntegrationTests.Infrastructure;

/// <summary>
/// This suite tests Library's OWN concurrency/state-machine invariants against a real Postgres
/// (Testcontainers) - Finance/Notifications/Student/Faculty are genuinely different modules with
/// their own already-verified test suites, so faking their cross-module contracts here keeps this
/// suite's own fixture focused on what it actually exercises, mirroring Hostel's own
/// <c>FakeCrossModuleAdapters</c> posture exactly.
/// </summary>
internal sealed class FakeInvoiceRequester : IInvoiceRequester
{
    public Task<Result<InvoiceSummary>> CreateInvoiceAsync(CreateInvoiceCommand command, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Success(new InvoiceSummary(Guid.NewGuid(), "Open", 0m, "BDT")));
}

internal sealed class FakeNotificationRequestIntake : INotificationRequestIntake
{
    public Task<Result<Guid>> SubmitAsync(SubmitNotificationRequestCommand request, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Success(Guid.NewGuid()));
}

/// <summary>Registers a per-Student <see cref="StudentAcademicStanding"/> for tests that need a specific status; any unregistered Student resolves to a generic "Active" standing so happy-path tests don't need explicit setup.</summary>
public sealed class FakeStudentStatusChecker : IStudentStatusChecker
{
    private readonly ConcurrentDictionary<Guid, StudentAcademicStanding> _byStudentId = new();

    public void Register(StudentAcademicStanding standing) => _byStudentId[standing.StudentId] = standing;

    public Task<StudentAcademicStanding?> GetByStudentIdAsync(Guid studentId, CancellationToken cancellationToken = default) =>
        Task.FromResult<StudentAcademicStanding?>(_byStudentId.TryGetValue(studentId, out var standing)
            ? standing
            : new StudentAcademicStanding(studentId, Guid.NewGuid(), Guid.NewGuid(), "Active", studentId));

    public Task<StudentAcademicStanding?> GetByUserIdAsync(Guid identityUserId, CancellationToken cancellationToken = default) =>
        Task.FromResult<StudentAcademicStanding?>(_byStudentId.Values.FirstOrDefault(s => s.IdentityUserId == identityUserId));
}

/// <summary>Registers a per-FacultyMember <see cref="FacultyMemberSummary"/> for tests that need a specific status; any unregistered FacultyMember id resolves to <see langword="null"/> (never a generic default) so a test's own <see cref="FakeStudentStatusChecker"/> registration is what determines whether a given caller resolves as a Student or a Faculty member.</summary>
public sealed class FakeFacultyMemberLookup : IFacultyMemberLookup
{
    private readonly ConcurrentDictionary<Guid, FacultyMemberSummary> _byId = new();
    private readonly ConcurrentDictionary<Guid, Guid> _userIdToFacultyMemberId = new();

    public void Register(FacultyMemberSummary summary, Guid identityUserId)
    {
        _byId[summary.Id] = summary;
        _userIdToFacultyMemberId[identityUserId] = summary.Id;
    }

    public Task<FacultyMemberSummary?> GetAsync(Guid facultyMemberId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_byId.TryGetValue(facultyMemberId, out var summary) ? summary : null);

    public Task<FacultyMemberSummary?> GetByUserIdAsync(Guid identityUserId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_userIdToFacultyMemberId.TryGetValue(identityUserId, out var facultyMemberId) && _byId.TryGetValue(facultyMemberId, out var summary)
            ? summary
            : null);
}
