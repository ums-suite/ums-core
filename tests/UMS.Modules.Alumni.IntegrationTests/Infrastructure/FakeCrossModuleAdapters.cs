using System.Collections.Concurrent;
using UMS.Shared.ErrorHandling.Results;
using UMS.Shared.Finance;
using UMS.Shared.Notifications;
using UMS.Shared.Student;

namespace UMS.Modules.Alumni.IntegrationTests.Infrastructure;

/// <summary>
/// This suite tests Alumni's OWN concurrency/state-machine/consumed-event invariants against a real
/// Postgres (Testcontainers) - Student/Finance/Notifications are genuinely different modules with
/// their own already-verified test suites, so faking their cross-module contracts here keeps this
/// suite's own fixture focused on what it actually exercises, mirroring Research's own
/// <c>FakeCrossModuleAdapters</c> posture exactly.
/// </summary>
public sealed class FakeStudentStatusChecker : IStudentStatusChecker
{
    private readonly ConcurrentDictionary<Guid, StudentAcademicStanding> _byStudentId = new();
    private readonly ConcurrentDictionary<Guid, Guid> _userIdToStudentId = new();

    public void Register(StudentAcademicStanding standing)
    {
        _byStudentId[standing.StudentId] = standing;
        if (standing.IdentityUserId is { } userId)
        {
            _userIdToStudentId[userId] = standing.StudentId;
        }
    }

    public Task<StudentAcademicStanding?> GetByStudentIdAsync(Guid studentId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_byStudentId.TryGetValue(studentId, out var standing) ? standing : null);

    public Task<StudentAcademicStanding?> GetByUserIdAsync(Guid identityUserId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_userIdToStudentId.TryGetValue(identityUserId, out var studentId) && _byStudentId.TryGetValue(studentId, out var standing) ? standing : null);
}

/// <summary>Always succeeds, minting a fresh InvoiceId - Alumni never verifies Finance's own invoice math, only that a reference comes back.</summary>
public sealed class FakeInvoiceRequester : IInvoiceRequester
{
    public List<CreateInvoiceCommand> Requests { get; } = [];

    public Task<Result<InvoiceSummary>> CreateInvoiceAsync(CreateInvoiceCommand command, CancellationToken cancellationToken = default)
    {
        Requests.Add(command);
        return Task.FromResult(Result.Success(new InvoiceSummary(Guid.NewGuid(), "Pending", 0m, command.FeeType)));
    }
}

internal sealed class FakeNotificationRequestIntake : INotificationRequestIntake
{
    public Task<Result<Guid>> SubmitAsync(SubmitNotificationRequestCommand request, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Success(Guid.NewGuid()));
}
