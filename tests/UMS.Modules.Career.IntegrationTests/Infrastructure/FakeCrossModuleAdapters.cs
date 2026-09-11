using System.Collections.Concurrent;
using UMS.Shared.Documents;
using UMS.Shared.ErrorHandling.Results;
using UMS.Shared.Notifications;
using UMS.Shared.Organization;
using UMS.Shared.Student;

namespace UMS.Modules.Career.IntegrationTests.Infrastructure;

/// <summary>
/// This suite tests Career's OWN concurrency/state-machine/consumed-event invariants against a real
/// Postgres (Testcontainers) - Student/Organization/Documents/Notifications are genuinely different
/// modules with their own already-verified test suites, so faking their cross-module contracts here
/// keeps this suite's own fixture focused on what it actually exercises, mirroring Alumni's own
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

/// <summary>Always reports existence - Career never verifies Organization's own Room data model, only that a venue reference is accepted or rejected on command.</summary>
public sealed class FakeRoomExistenceChecker : IRoomExistenceChecker
{
    public bool AlwaysExists { get; set; } = true;

    public Task<bool> ExistsAsync(Guid roomId, CancellationToken cancellationToken = default) => Task.FromResult(AlwaysExists);
}

/// <summary>Always mints a fresh, immediately-`Ready` artifact - Career never verifies Documents' own storage/checksum mechanics, only that an uploaded resume reference comes back.</summary>
public sealed class FakeUploadedArtifactRequester : IUploadedArtifactRequester
{
    public Task<Result<UploadedArtifactSlot>> RequestUploadAsync(RequestUploadedArtifactCommand command, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Success(new UploadedArtifactSlot(Guid.NewGuid(), "PendingUpload", "https://fake-storage.example/upload")));

    public Task<Result<UploadedArtifactReference>> ConfirmAsync(Guid artifactId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Success(new UploadedArtifactReference(artifactId, "Ready", "application/pdf", 12345)));
}

public sealed class FakeNotificationRequestIntake : INotificationRequestIntake
{
    public ConcurrentQueue<SubmitNotificationRequestCommand> Requests { get; } = new();

    public Task<Result<Guid>> SubmitAsync(SubmitNotificationRequestCommand request, CancellationToken cancellationToken = default)
    {
        Requests.Enqueue(request);
        return Task.FromResult(Result.Success(Guid.NewGuid()));
    }
}
