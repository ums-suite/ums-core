using System.Collections.Concurrent;
using UMS.Modules.Content.Application.Abstractions;
using UMS.Shared.Documents;
using UMS.Shared.ErrorHandling.Results;
using UMS.Shared.Identity;
using UMS.Shared.Notifications;
using UMS.Shared.Organization;

namespace UMS.Modules.Content.IntegrationTests.Infrastructure;

/// <summary>No real Redis in this fixture (Application-service-level suite, no CDN/cache concern under test here) - a no-op stands in for the real `RedisCacheInvalidator`, which needs a live `IConnectionMultiplexer` this fixture deliberately doesn't stand up.</summary>
internal sealed class FakeCacheInvalidator : ICacheInvalidator
{
    public Task InvalidateAsync(string cacheKey, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

/// <summary>
/// This suite tests Content's OWN concurrency/state-machine/scoping invariants against a real
/// Postgres (Testcontainers) - Identity/Organization/Documents/Notifications are genuinely
/// different modules with their own already-verified test suites, so faking their cross-module
/// contracts here keeps this suite's own fixture focused on what it actually exercises, mirroring
/// Hostel's/Admission's own <c>FakeCrossModuleAdapters</c> posture exactly.
/// </summary>
public sealed class FakeScopeGrantDirectory : IScopeGrantDirectory
{
    private readonly ConcurrentDictionary<(Guid UserId, string Permission, Guid OrganizationNodeId), bool> _grants = new();

    public void Grant(Guid userId, string permission, Guid organizationNodeId) => _grants[(userId, permission, organizationNodeId)] = true;

    public Task<bool> HasPermissionAtScopeAsync(Guid userId, string permission, Guid organizationNodeId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_grants.ContainsKey((userId, permission, organizationNodeId)));

    public Task<IReadOnlyCollection<Guid>> GetUserIdsWithPermissionAtScopeAsync(string permission, Guid organizationNodeId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyCollection<Guid>>(_grants.Keys
            .Where(k => k.Permission == permission && k.OrganizationNodeId == organizationNodeId)
            .Select(k => k.UserId)
            .ToList());
}

/// <summary>Registers a set of "existing" node ids explicitly - any unregistered id resolves as not-existing, exercising HomepageSectionService's graceful-degradation path (edge-cases.md "Organization reference later deleted/deactivated") without any special-casing.</summary>
public sealed class FakeOrganizationNodeExistenceChecker : IOrganizationNodeExistenceChecker
{
    private readonly ConcurrentDictionary<Guid, bool> _existing = new();

    public void Register(Guid organizationNodeId) => _existing[organizationNodeId] = true;

    public Task<bool> ExistsAsync(Guid organizationNodeId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_existing.ContainsKey(organizationNodeId));
}

/// <summary>Every artifact resolves as already `Ready` unless explicitly registered otherwise via <see cref="MarkNotReady"/> (CNT-11's "the file upload must complete before a DownloadResource can reference it" gate).</summary>
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
