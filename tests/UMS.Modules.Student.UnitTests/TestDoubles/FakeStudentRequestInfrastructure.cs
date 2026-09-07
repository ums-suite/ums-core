using UMS.Modules.Student.Application.Abstractions;
using UMS.Modules.Student.Domain.BulkImport;
using UMS.Modules.Student.Domain.StudentRequests;

namespace UMS.Modules.Student.UnitTests.TestDoubles;

/// <summary>In-memory test doubles for STU-9..STU-16's own Application-layer abstractions - mirrors <c>FakeStudentInfrastructure.cs</c>'s exact pattern.</summary>
public sealed class FakeStudentRequestRepository : IStudentRequestRepository
{
    private readonly Dictionary<Guid, StudentRequest> _requests = [];

    public void Seed(StudentRequest request) => _requests[request.Id.Value] = request;

    public Task<StudentRequest?> GetByIdAsync(StudentRequestId id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_requests.GetValueOrDefault(id.Value));

    public Task<StudentRequest?> GetOpenByStudentAndTypeAsync(Guid studentId, StudentRequestType requestType, CancellationToken cancellationToken = default) =>
        Task.FromResult(_requests.Values.FirstOrDefault(r =>
            r.StudentId == studentId &&
            r.RequestType == requestType &&
            (r.Status == StudentRequestStatus.Submitted || r.Status == StudentRequestStatus.UnderReview)));

    public void Add(StudentRequest request) => Seed(request);
}

public sealed class FakeReviewerScopeDirectory : IReviewerScopeDirectory
{
    private readonly HashSet<(Guid UserId, string Permission, Guid ScopeNodeId)> _grants = [];
    private readonly Dictionary<(string Permission, Guid ScopeNodeId), List<Guid>> _holders = [];

    public void Grant(Guid userId, string permission, Guid organizationNodeId)
    {
        _grants.Add((userId, permission, organizationNodeId));
        var key = (permission, organizationNodeId);
        if (!_holders.TryGetValue(key, out var list))
        {
            list = [];
            _holders[key] = list;
        }

        list.Add(userId);
    }

    public Task<bool> HasPermissionAtScopeAsync(Guid userId, string permission, Guid organizationNodeId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_grants.Contains((userId, permission, organizationNodeId)));

    public Task<IReadOnlyCollection<Guid>> GetUserIdsWithPermissionAtScopeAsync(string permission, Guid organizationNodeId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyCollection<Guid>>(_holders.TryGetValue((permission, organizationNodeId), out var list) ? list : []);
}

public sealed class FakeDepartmentFacultyLookup(Guid? parentFacultyId) : IDepartmentFacultyLookup
{
    public Task<Guid?> GetParentFacultyIdAsync(Guid departmentId, CancellationToken cancellationToken = default) =>
        Task.FromResult(parentFacultyId);
}

public sealed class FakeAcademicTranscriptPort : IAcademicTranscriptPort
{
    public Task<TranscriptSnapshot> GetTranscriptAsync(Guid studentId, CancellationToken cancellationToken = default) =>
        Task.FromResult(new TranscriptSnapshot(studentId, [], null));
}

public sealed class FakeStudentBulkImportJobRepository : IStudentBulkImportJobRepository
{
    private readonly Dictionary<Guid, StudentBulkImportJob> _jobs = [];

    public void Seed(StudentBulkImportJob job) => _jobs[job.Id.Value] = job;

    public Task<StudentBulkImportJob?> GetByIdAsync(StudentBulkImportJobId id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_jobs.GetValueOrDefault(id.Value));

    public Task<IReadOnlyList<StudentBulkImportJobId>> GetActiveAsync(int batchSize, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<StudentBulkImportJobId>>(_jobs.Values
            .Where(j => j.Status is StudentBulkImportJobStatus.Approved or StudentBulkImportJobStatus.Processing)
            .OrderBy(j => j.CreatedAt)
            .Take(batchSize)
            .Select(j => j.Id)
            .ToList());

    public void Add(StudentBulkImportJob job) => Seed(job);
}

public sealed class FakeStudentBulkImportRowRepository : IStudentBulkImportRowRepository
{
    private readonly List<StudentBulkImportRow> _rows = [];

    public Task<IReadOnlyList<StudentBulkImportRow>> GetUnresolvedBatchAsync(StudentBulkImportJobId jobId, int batchSize, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<StudentBulkImportRow>>(_rows
            .Where(r => r.JobId == jobId && r.Status == StudentBulkImportRowStatus.Valid)
            .OrderBy(r => r.RowNumber)
            .Take(batchSize)
            .ToList());

    public Task<IReadOnlyList<StudentBulkImportRow>> ListByJobAsync(StudentBulkImportJobId jobId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<StudentBulkImportRow>>(_rows.Where(r => r.JobId == jobId).OrderBy(r => r.RowNumber).ToList());

    public void AddRange(IEnumerable<StudentBulkImportRow> rows) => _rows.AddRange(rows);
}
