using System.Data.Common;
using UMS.Modules.Organization.Application.Abstractions;
using UMS.Modules.Organization.Domain.Campuses;
using UMS.Modules.Organization.Domain.Common;
using UMS.Modules.Organization.Domain.Departments;
using UMS.Modules.Organization.Domain.Facilities;
using UMS.Modules.Organization.Domain.Faculties;
using UMS.Modules.Organization.Domain.Universities;
using UMS.Shared.Audit;
using UMS.Shared.ErrorHandling.Results;
using OrgProgram = UMS.Modules.Organization.Domain.Programs.Program;
using OrgProgramId = UMS.Modules.Organization.Domain.Programs.ProgramId;

namespace UMS.Modules.Organization.UnitTests.TestDoubles;

/// <summary>
/// Minimal in-memory test doubles for the Application-layer abstractions, so Organization's
/// Application services (parent-active-status validation, cascade blocking, hard-delete
/// restriction) can be exercised as pure unit tests, without a real Postgres/Redis - mirrors
/// Identity's own <c>TestDoubles/FakeIdentityInfrastructure.cs</c> approach. The uniqueness
/// constraint and optimistic-concurrency invariants are deliberately NOT reproduced here (they are
/// DB-enforced per design-decisions.md, not application-level logic) - those are exercised for
/// real in the Testcontainers-backed integration test suite instead.
/// </summary>
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
        // No-op: this fake never actually persists, so there is no concurrency token to prime.
        // The optimistic-concurrency conflict path is exercised for real in the integration suite.
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

public sealed class FakeOrganizationTreeCache : IOrganizationTreeCache
{
    public Task<string?> GetTreeJsonAsync(Guid? rootId, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);

    public Task SetTreeJsonAsync(Guid? rootId, string json, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<string?> GetAncestorsJsonAsync(Guid nodeId, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);

    public Task SetAncestorsJsonAsync(Guid nodeId, string json, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task InvalidateAsync(Guid mutatedNodeId, IReadOnlyCollection<Guid> ancestorIds, IReadOnlyCollection<Guid>? descendantIds = null, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}

public sealed class FakeFacultyEmploymentChecker(bool hasActiveFacultyMember = false) : IFacultyEmploymentChecker
{
    public Task<bool> HasActiveFacultyMemberAsync(Guid departmentId, CancellationToken cancellationToken = default) =>
        Task.FromResult(hasActiveFacultyMember);
}

public sealed class FakeRoomReferenceChecker(bool hasReferences = false) : IRoomReferenceChecker
{
    public Task<bool> HasAnyReferencesAsync(Guid roomId, CancellationToken cancellationToken = default) =>
        Task.FromResult(hasReferences);
}

public sealed class FakeUniversityRepository : IUniversityRepository
{
    private readonly Dictionary<Guid, University> _universities = [];

    public void Seed(University university) => _universities[university.Id.Value] = university;

    public Task<University?> GetByIdAsync(UniversityId id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_universities.GetValueOrDefault(id.Value));

    public Task<University?> GetByIdForUpdateAsync(UniversityId id, CancellationToken cancellationToken = default) => GetByIdAsync(id, cancellationToken);

    public Task<IReadOnlyList<University>> ListAsync(int skip, int take, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<University>>(_universities.Values.Skip(skip).Take(take).ToList());

    public Task<int> CountAsync(CancellationToken cancellationToken = default) => Task.FromResult(_universities.Count);

    public void Add(University university) => Seed(university);
}

public sealed class FakeCampusRepository : ICampusRepository
{
    private readonly Dictionary<Guid, Campus> _campuses = [];

    public void Seed(Campus campus) => _campuses[campus.Id.Value] = campus;

    public Task<Campus?> GetByIdAsync(CampusId id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_campuses.GetValueOrDefault(id.Value));

    public Task<Campus?> GetByIdForUpdateAsync(CampusId id, CancellationToken cancellationToken = default) => GetByIdAsync(id, cancellationToken);

    public Task<bool> HasAnyActiveUnderAsync(UniversityId universityId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_campuses.Values.Any(c => c.UniversityId == universityId && c.Status == NodeStatus.Active));

    public Task<IReadOnlyList<Campus>> ListAsync(UniversityId? universityId, int skip, int take, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Campus>>(_campuses.Values.Where(c => universityId == null || c.UniversityId == universityId).Skip(skip).Take(take).ToList());

    public Task<int> CountAsync(UniversityId? universityId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_campuses.Values.Count(c => universityId == null || c.UniversityId == universityId));

    public void Add(Campus campus) => Seed(campus);
}

public sealed class FakeFacultyRepository : IFacultyRepository
{
    private readonly Dictionary<Guid, Faculty> _faculties = [];

    public void Seed(Faculty faculty) => _faculties[faculty.Id.Value] = faculty;

    public Task<Faculty?> GetByIdAsync(FacultyId id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_faculties.GetValueOrDefault(id.Value));

    public Task<Faculty?> GetByIdForUpdateAsync(FacultyId id, CancellationToken cancellationToken = default) => GetByIdAsync(id, cancellationToken);

    public Task<bool> HasAnyActiveUnderAsync(CampusId campusId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_faculties.Values.Any(f => f.CampusId == campusId && f.Status == NodeStatus.Active));

    public Task<IReadOnlyList<Faculty>> ListAsync(CampusId? campusId, int skip, int take, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Faculty>>(_faculties.Values.Where(f => campusId == null || f.CampusId == campusId).Skip(skip).Take(take).ToList());

    public Task<int> CountAsync(CampusId? campusId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_faculties.Values.Count(f => campusId == null || f.CampusId == campusId));

    public Task<IReadOnlyList<Faculty>> ListAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Faculty>>(_faculties.Values.ToList());

    public void Add(Faculty faculty) => Seed(faculty);
}

public sealed class FakeDepartmentRepository : IDepartmentRepository
{
    private readonly Dictionary<Guid, Department> _departments = [];

    public void Seed(Department department) => _departments[department.Id.Value] = department;

    public Task<Department?> GetByIdAsync(DepartmentId id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_departments.GetValueOrDefault(id.Value));

    public Task<Department?> GetByIdForUpdateAsync(DepartmentId id, CancellationToken cancellationToken = default) => GetByIdAsync(id, cancellationToken);

    public Task<bool> HasAnyActiveUnderAsync(FacultyId facultyId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_departments.Values.Any(d => d.FacultyId == facultyId && d.Status == NodeStatus.Active));

    public Task<IReadOnlyList<Department>> ListAsync(FacultyId? facultyId, int skip, int take, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Department>>(_departments.Values.Where(d => facultyId == null || d.FacultyId == facultyId).Skip(skip).Take(take).ToList());

    public Task<int> CountAsync(FacultyId? facultyId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_departments.Values.Count(d => facultyId == null || d.FacultyId == facultyId));

    public Task<IReadOnlyList<Department>> ListAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Department>>(_departments.Values.ToList());

    public void Add(Department department) => Seed(department);
}

public sealed class FakeProgramRepository : IProgramRepository
{
    private readonly Dictionary<Guid, OrgProgram> _programs = [];

    public void Seed(OrgProgram program) => _programs[program.Id.Value] = program;

    public Task<OrgProgram?> GetByIdAsync(OrgProgramId id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_programs.GetValueOrDefault(id.Value));

    public Task<IReadOnlyList<OrgProgram>> ListAsync(DepartmentId? departmentId, int skip, int take, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<OrgProgram>>(_programs.Values.Where(p => departmentId == null || p.DepartmentId == departmentId).Skip(skip).Take(take).ToList());

    public Task<int> CountAsync(DepartmentId? departmentId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_programs.Values.Count(p => departmentId == null || p.DepartmentId == departmentId));

    public Task<IReadOnlyList<OrgProgram>> ListAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<OrgProgram>>(_programs.Values.ToList());

    public void Add(OrgProgram program) => Seed(program);
}

public sealed class FakeBuildingRepository : IBuildingRepository
{
    private readonly Dictionary<Guid, Building> _buildings = [];

    public void Seed(Building building) => _buildings[building.Id.Value] = building;

    public Task<Building?> GetByIdAsync(BuildingId id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_buildings.GetValueOrDefault(id.Value));

    public Task<Building?> GetByIdForUpdateAsync(BuildingId id, CancellationToken cancellationToken = default) => GetByIdAsync(id, cancellationToken);

    public Task<IReadOnlyList<Building>> ListAsync(CampusId? campusId, int skip, int take, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Building>>(_buildings.Values.Where(b => campusId == null || b.CampusId == campusId).Skip(skip).Take(take).ToList());

    public Task<int> CountAsync(CampusId? campusId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_buildings.Values.Count(b => campusId == null || b.CampusId == campusId));

    public void Add(Building building) => Seed(building);

    public void Remove(Building building) => _buildings.Remove(building.Id.Value);
}

public sealed class FakeRoomRepository : IRoomRepository
{
    private readonly Dictionary<Guid, Room> _rooms = [];

    public void Seed(Room room) => _rooms[room.Id.Value] = room;

    public Task<Room?> GetByIdAsync(RoomId id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_rooms.GetValueOrDefault(id.Value));

    public Task<bool> HasAnyUnderAsync(BuildingId buildingId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_rooms.Values.Any(r => r.BuildingId == buildingId));

    public Task<IReadOnlyList<Room>> ListByBuildingAsync(BuildingId buildingId, int skip, int take, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Room>>(_rooms.Values.Where(r => r.BuildingId == buildingId).Skip(skip).Take(take).ToList());

    public Task<int> CountByBuildingAsync(BuildingId buildingId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_rooms.Values.Count(r => r.BuildingId == buildingId));

    public void Add(Room room) => Seed(room);

    public void Remove(Room room) => _rooms.Remove(room.Id.Value);
}
