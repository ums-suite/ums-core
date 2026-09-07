using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Learning.Application.Abstractions;

/// <summary>
/// LRN-5/LRN-13: this module's own port over Documents' shared
/// <c>UMS.Shared.Documents.IUploadedArtifactRequester</c> contract - the same module-local-port
/// shape Documents' own <c>INotificationRequestPublisher</c> uses over Notifications' shared
/// intake, so the Application layer depends on a Learning-owned abstraction while the
/// Infrastructure adapter does the cross-module translation.
///
/// <para>
/// Learning stores only the returned <c>artifactId</c>. There is deliberately no object-storage
/// client, bucket name, or presigning code anywhere in this module (design-decisions.md, "Raw File
/// Storage via Documents' Object-Storage Integration": standing up an independent integration is
/// "a direct, explicit violation" of that decision).
/// </para>
/// </summary>
public interface IUploadedArtifactGateway
{
    public Task<Result<ArtifactUploadSlot>> RequestUploadAsync(Guid ownerUserId, string artifactType, string mimeType, CancellationToken cancellationToken = default);

    public Task<Result<ArtifactConfirmation>> ConfirmAsync(Guid artifactId, CancellationToken cancellationToken = default);
}

public sealed record ArtifactUploadSlot(Guid ArtifactId, string Status, string? UploadUrl);

/// <summary><paramref name="IsReady"/> is the only thing a Learning call site should branch on - an artifact that isn't Ready is not a usable reference (documents requirement-spec.md §4).</summary>
public sealed record ArtifactConfirmation(Guid ArtifactId, string Status, string MimeType, long? SizeBytes, bool IsReady);
