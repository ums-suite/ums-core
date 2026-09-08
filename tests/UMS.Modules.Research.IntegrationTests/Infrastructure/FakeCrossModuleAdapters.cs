using System.Collections.Concurrent;
using UMS.Shared.Documents;
using UMS.Shared.ErrorHandling.Results;
using UMS.Shared.Faculty;
using UMS.Shared.Notifications;

namespace UMS.Modules.Research.IntegrationTests.Infrastructure;

/// <summary>
/// This suite tests Research's OWN concurrency/state-machine/consumed-event invariants against a real
/// Postgres (Testcontainers) - Faculty/Documents/Notifications are genuinely different modules with
/// their own already-verified test suites, so faking their cross-module contracts here keeps this
/// suite's own fixture focused on what it actually exercises, mirroring Library's/Hostel's own
/// <c>FakeCrossModuleAdapters</c> posture exactly.
/// </summary>
public sealed class FakeFacultyMemberLookup : IFacultyMemberLookup
{
    private readonly ConcurrentDictionary<Guid, FacultyMemberSummary> _byId = new();
    private readonly ConcurrentDictionary<Guid, Guid> _userIdToFacultyMemberId = new();

    public void Register(FacultyMemberSummary summary, Guid? identityUserId = null)
    {
        _byId[summary.Id] = summary;
        if (identityUserId is { } userId)
        {
            _userIdToFacultyMemberId[userId] = summary.Id;
        }
    }

    public Task<FacultyMemberSummary?> GetAsync(Guid facultyMemberId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_byId.TryGetValue(facultyMemberId, out var summary) ? summary : null);

    public Task<FacultyMemberSummary?> GetByUserIdAsync(Guid identityUserId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_userIdToFacultyMemberId.TryGetValue(identityUserId, out var facultyMemberId) && _byId.TryGetValue(facultyMemberId, out var summary)
            ? summary
            : null);
}

/// <summary>Every artifact resolves as already `Ready` unless explicitly registered otherwise.</summary>
public sealed class FakeUploadedArtifactRequester : IUploadedArtifactRequester
{
    private readonly ConcurrentDictionary<Guid, string> _statusOverrides = new();

    public void MarkNotReady(Guid artifactId) => _statusOverrides[artifactId] = "Pending";

    public Task<Result<UploadedArtifactSlot>> RequestUploadAsync(RequestUploadedArtifactCommand command, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Success(new UploadedArtifactSlot(Guid.NewGuid(), "Pending", "https://fake-upload/put")));

    public Task<Result<UploadedArtifactReference>> ConfirmAsync(Guid artifactId, CancellationToken cancellationToken = default)
    {
        var status = _statusOverrides.TryGetValue(artifactId, out var overridden) ? overridden : "Ready";
        return Task.FromResult(Result.Success(new UploadedArtifactReference(artifactId, status, "application/pdf", 1024)));
    }
}

internal sealed class FakeNotificationRequestIntake : INotificationRequestIntake
{
    public Task<Result<Guid>> SubmitAsync(SubmitNotificationRequestCommand request, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Success(Guid.NewGuid()));
}
