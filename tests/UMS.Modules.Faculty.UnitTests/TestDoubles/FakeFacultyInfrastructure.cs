using System.Data.Common;
using UMS.Modules.Faculty.Application.Abstractions;
using UMS.Modules.Faculty.Domain.CourseAssignments;
using UMS.Modules.Faculty.Domain.FacultyMembers;
using UMS.Modules.Faculty.Domain.LeaveRequests;
using UMS.Modules.Faculty.Domain.ResearchProfiles;
using UMS.Shared.Audit;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Faculty.UnitTests.TestDoubles;

/// <summary>Minimal in-memory test doubles for the Application-layer abstractions - mirrors Organization's own <c>TestDoubles/FakeOrganizationInfrastructure.cs</c> exactly.</summary>
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

public sealed class FakeNotificationRequestPublisher : INotificationRequestPublisher
{
    public List<NotificationRequest> Published { get; } = [];

    public Task PublishAsync(NotificationRequest request, CancellationToken cancellationToken = default)
    {
        Published.Add(request);
        return Task.CompletedTask;
    }
}

public sealed class FakeFacultyMemberRepository : IFacultyMemberRepository
{
    private readonly Dictionary<Guid, FacultyMember> _facultyMembers = [];

    public void Seed(FacultyMember facultyMember) => _facultyMembers[facultyMember.Id.Value] = facultyMember;

    public Task<FacultyMember?> GetByIdAsync(FacultyMemberId id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_facultyMembers.GetValueOrDefault(id.Value));

    public Task<FacultyMember?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_facultyMembers.Values.FirstOrDefault(f => f.UserId == userId));

    public Task<bool> HasAnyActiveInDepartmentAsync(Guid departmentId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_facultyMembers.Values.Any(f => f.DepartmentId == departmentId && f.Status == FacultyMemberStatus.Active));

    public Task<IReadOnlyList<FacultyMember>> ListAsync(Guid? departmentId, int skip, int take, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<FacultyMember>>(_facultyMembers.Values.Where(f => departmentId == null || f.DepartmentId == departmentId).Skip(skip).Take(take).ToList());

    public Task<int> CountAsync(Guid? departmentId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_facultyMembers.Values.Count(f => departmentId == null || f.DepartmentId == departmentId));

    public void Add(FacultyMember facultyMember) => Seed(facultyMember);
}

public sealed class FakeCourseAssignmentRepository : ICourseAssignmentRepository
{
    private readonly Dictionary<(Guid, Guid), CourseAssignment> _courseAssignments = [];

    public void Seed(CourseAssignment courseAssignment) => _courseAssignments[(courseAssignment.FacultyMemberId, courseAssignment.CourseOfferingId)] = courseAssignment;

    public Task<CourseAssignment?> GetForUpdateAsync(Guid facultyMemberId, Guid courseOfferingId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_courseAssignments.GetValueOrDefault((facultyMemberId, courseOfferingId)));

    public Task<IReadOnlyList<CourseAssignment>> ListByFacultyMemberAsync(Guid facultyMemberId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<CourseAssignment>>(_courseAssignments.Values.Where(c => c.FacultyMemberId == facultyMemberId).ToList());

    public void Add(CourseAssignment courseAssignment) => Seed(courseAssignment);
}

public sealed class FakeLeaveRequestRepository : ILeaveRequestRepository
{
    private readonly Dictionary<Guid, LeaveRequest> _leaveRequests = [];

    public void Seed(LeaveRequest leaveRequest) => _leaveRequests[leaveRequest.Id.Value] = leaveRequest;

    public Task<LeaveRequest?> GetByIdAsync(LeaveRequestId id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_leaveRequests.GetValueOrDefault(id.Value));

    public Task<IReadOnlyList<LeaveRequest>> ListByFacultyMemberAsync(Guid facultyMemberId, int skip, int take, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<LeaveRequest>>(_leaveRequests.Values.Where(l => l.FacultyMemberId == facultyMemberId).Skip(skip).Take(take).ToList());

    public void Add(LeaveRequest leaveRequest) => Seed(leaveRequest);
}

public sealed class FakeResearchProfileRepository : IResearchProfileRepository
{
    private readonly Dictionary<Guid, ResearchProfile> _researchProfiles = [];

    public void Seed(ResearchProfile researchProfile) => _researchProfiles[researchProfile.FacultyMemberId] = researchProfile;

    public Task<ResearchProfile?> GetByFacultyMemberIdAsync(Guid facultyMemberId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_researchProfiles.GetValueOrDefault(facultyMemberId));

    public void Add(ResearchProfile researchProfile) => Seed(researchProfile);
}
