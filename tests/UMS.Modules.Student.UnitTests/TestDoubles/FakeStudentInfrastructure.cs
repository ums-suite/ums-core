using System.Data.Common;
using UMS.Modules.Student.Application.Abstractions;
using UMS.Shared.Audit;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Student.UnitTests.TestDoubles;

/// <summary>Minimal in-memory test doubles for the Application-layer abstractions - mirrors Faculty's own <c>TestDoubles/FakeFacultyInfrastructure.cs</c> exactly.</summary>
public sealed class FakeClock : IClock
{
    public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class FakeUmsTransaction : IUmsTransaction
{
    public bool Committed { get; private set; }

    public bool RolledBack { get; private set; }

    public DbTransaction DbTransaction => null!;

    public Task CommitAsync(CancellationToken cancellationToken = default)
    {
        Committed = true;
        return Task.CompletedTask;
    }

    public Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        RolledBack = true;
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

public sealed class FakeUnitOfWork : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);

    public Task<IUmsTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IUmsTransaction>(new FakeUmsTransaction());

    public void SetExpectedVersion<TEntity>(TEntity entity, uint expectedVersion)
        where TEntity : class
    {
        // No-op - the optimistic-concurrency conflict path is exercised for real in the
        // integration suite (design-decisions.md's own layered-verification convention).
    }
}

public sealed class FakeAuditRecorder : IAuditRecorder
{
    public List<RecordAuditEntryRequest> RecordedEntries { get; } = [];

    public Task<Result> RecordEntryAsync(RecordAuditEntryRequest request, DbTransaction hostTransaction, CancellationToken cancellationToken = default)
    {
        RecordedEntries.Add(request);
        return Task.FromResult(Result.Success());
    }

    public Task<Result> RecordEntriesAsync(IReadOnlyCollection<RecordAuditEntryRequest> requests, DbTransaction hostTransaction, CancellationToken cancellationToken = default)
    {
        RecordedEntries.AddRange(requests);
        return Task.FromResult(Result.Success());
    }
}

public sealed class FakeOrganizationDepartmentExistenceChecker(bool exists = true) : IOrganizationDepartmentExistenceChecker
{
    public Task<bool> ExistsAsync(Guid departmentId, CancellationToken cancellationToken = default) => Task.FromResult(exists);
}

public sealed class FakeProgramExistenceChecker(bool exists = true) : IProgramExistenceChecker
{
    public Task<bool> ExistsAsync(Guid programId, CancellationToken cancellationToken = default) => Task.FromResult(exists);
}

public sealed class FakeStudentNumberSequence : IStudentNumberSequence
{
    private readonly Dictionary<string, long> _counters = [];

    public Task<long> NextAsync(int admissionYear, string facultyCode, CancellationToken cancellationToken = default)
    {
        var key = $"{admissionYear}:{facultyCode}";
        var next = _counters.TryGetValue(key, out var current) ? current + 1 : 1;
        _counters[key] = next;
        return Task.FromResult(next);
    }
}

public sealed class FakeUserProvisioningPort(bool succeeds = true) : IUserProvisioningPort
{
    public List<ProvisionStudentUserRequest> Requests { get; } = [];

    public Task<UserProvisioningOutcome> ProvisionAsync(ProvisionStudentUserRequest request, CancellationToken cancellationToken = default)
    {
        Requests.Add(request);
        return Task.FromResult(succeeds ? new UserProvisioningOutcome(true, Guid.NewGuid(), null) : new UserProvisioningOutcome(false, null, "provisioning failed"));
    }
}

public sealed class FakeDocumentGenerationPort(bool succeeds = true) : IDocumentGenerationPort
{
    public List<RequestStudentIdCardRequest> Requests { get; } = [];

    public Task<DocumentGenerationOutcome> RequestIdCardAsync(RequestStudentIdCardRequest request, CancellationToken cancellationToken = default)
    {
        Requests.Add(request);
        return Task.FromResult(succeeds ? new DocumentGenerationOutcome(true, Guid.NewGuid(), null) : new DocumentGenerationOutcome(false, null, "generation failed"));
    }
}

public sealed class FakeNotificationRequestPublisher : INotificationRequestPublisher
{
    public List<NotificationRequest> Published { get; } = [];

    public Task PublishAsync(NotificationRequest request, CancellationToken cancellationToken = default)
    {
        Published.Add(request);
        return Task.CompletedTask;
    }
}

public sealed class FakeStudentRepository : IStudentRepository
{
    private readonly Dictionary<Guid, Domain.Students.Student> _students = [];

    public void Seed(Domain.Students.Student student) => _students[student.Id.Value] = student;

    public Task<Domain.Students.Student?> GetByIdAsync(Domain.Students.StudentId id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_students.GetValueOrDefault(id.Value));

    public Task<Domain.Students.Student?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_students.Values.FirstOrDefault(s => s.IdentityUserId == userId));

    public Task<Domain.Students.Student?> GetByOriginatingApplicationIdAsync(Guid originatingApplicationId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_students.Values.FirstOrDefault(s => s.OriginatingApplicationId == originatingApplicationId));

    public void Add(Domain.Students.Student student) => Seed(student);
}
