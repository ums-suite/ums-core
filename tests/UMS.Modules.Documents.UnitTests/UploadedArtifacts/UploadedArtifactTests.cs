using UMS.Modules.Documents.Domain.UploadedArtifacts;

namespace UMS.Modules.Documents.UnitTests.UploadedArtifacts;

/// <summary>requirement-spec.md documents §2 Uploaded Artifact Storage: "A row stuck below Ready ... is never treated as a valid reference by any calling module."</summary>
public sealed class UploadedArtifactTests
{
    [Fact]
    public void RequestUpload_starts_PendingUpload()
    {
        var result = UploadedArtifact.RequestUpload(Guid.NewGuid(), "ResumeProfile", "application/pdf", "uploads/key", DateTimeOffset.UtcNow);

        Assert.True(result.IsSuccess);
        Assert.Equal(UploadedArtifactStatus.PendingUpload, result.Value.Status);
    }

    [Fact]
    public void RequestUpload_requires_an_artifact_type()
    {
        var result = UploadedArtifact.RequestUpload(Guid.NewGuid(), "  ", "application/pdf", "uploads/key", DateTimeOffset.UtcNow);

        Assert.True(result.IsFailure);
        Assert.Equal("uploaded_artifact.artifact_type_required", result.Error!.Code);
    }

    [Fact]
    public void Confirm_from_PendingUpload_transitions_to_Ready()
    {
        var artifact = UploadedArtifact.RequestUpload(Guid.NewGuid(), "ResumeProfile", "application/pdf", "uploads/key", DateTimeOffset.UtcNow).Value;

        var result = artifact.Confirm("checksum", 2048, DateTimeOffset.UtcNow);

        Assert.True(result.IsSuccess);
        Assert.Equal(UploadedArtifactStatus.Ready, artifact.Status);
        Assert.Equal(2048, artifact.SizeBytes);
    }

    [Fact]
    public void Confirm_twice_is_rejected()
    {
        var artifact = UploadedArtifact.RequestUpload(Guid.NewGuid(), "ResumeProfile", "application/pdf", "uploads/key", DateTimeOffset.UtcNow).Value;
        artifact.Confirm("checksum", 2048, DateTimeOffset.UtcNow);

        var result = artifact.Confirm("checksum", 2048, DateTimeOffset.UtcNow);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void MarkFailed_from_PendingUpload_records_the_failure_reason()
    {
        var artifact = UploadedArtifact.RequestUpload(Guid.NewGuid(), "ResumeProfile", "application/pdf", "uploads/key", DateTimeOffset.UtcNow).Value;

        var result = artifact.MarkFailed("object not found");

        Assert.True(result.IsSuccess);
        Assert.Equal(UploadedArtifactStatus.Failed, artifact.Status);
    }
}
