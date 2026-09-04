using UMS.Modules.Documents.Domain.Common;
using UMS.Modules.Documents.Domain.GeneratedDocuments;
using UMS.Modules.Documents.Domain.Templates;

namespace UMS.Modules.Documents.UnitTests.GeneratedDocuments;

/// <summary>
/// requirement-spec.md documents §4's core invariants: the three-way Ready gate (render + upload +
/// checksum), the Pending/Uploaded/Ready/Revoked/Superseded/Failed lifecycle, and the natural-key
/// claim/reopen mechanics edge-cases.md's idempotent-generation race decision depends on.
/// </summary>
public sealed class GeneratedDocumentTests
{
    private static GeneratedDocument Claim() => GeneratedDocument.Claim(
        ownerId: Guid.NewGuid(),
        documentType: DocumentType.Transcript,
        sourceReferenceId: Guid.NewGuid(),
        templateId: DocumentTemplateId.New(),
        templateVersion: 1,
        digitalVerificationId: "VERIFY123",
        renderDataJson: "{}",
        language: LanguageCode.En,
        now: DateTimeOffset.UtcNow);

    [Fact]
    public void Claim_starts_in_Pending_status()
    {
        var document = Claim();

        Assert.Equal(GeneratedDocumentStatus.Pending, document.Status);
        Assert.Null(document.StorageKey);
        Assert.Null(document.Checksum);
        Assert.Null(document.ReadyAt);
    }

    [Fact]
    public void AttachUploadedArtifact_from_Pending_transitions_to_Uploaded_but_not_Ready()
    {
        var document = Claim();

        var result = document.AttachUploadedArtifact("key/1.pdf", "sha256hash", "application/pdf", 1024);

        Assert.True(result.IsSuccess);
        Assert.Equal(GeneratedDocumentStatus.Uploaded, document.Status);
        Assert.Equal("key/1.pdf", document.StorageKey);
        Assert.Null(document.ReadyAt);
    }

    [Fact]
    public void MarkReady_before_an_artifact_is_attached_is_rejected()
    {
        var document = Claim();

        var result = document.MarkReady(DateTimeOffset.UtcNow);

        Assert.True(result.IsFailure);
        Assert.Equal(GeneratedDocumentStatus.Pending, document.Status);
    }

    [Fact]
    public void MarkReady_after_AttachUploadedArtifact_succeeds_and_stamps_ReadyAt()
    {
        var document = Claim();
        document.AttachUploadedArtifact("key/1.pdf", "sha256hash", "application/pdf", 1024);

        var result = document.MarkReady(DateTimeOffset.UtcNow);

        Assert.True(result.IsSuccess);
        Assert.Equal(GeneratedDocumentStatus.Ready, document.Status);
        Assert.NotNull(document.ReadyAt);
    }

    [Fact]
    public void MarkFailed_from_Pending_clears_any_partial_artifact_reference()
    {
        var document = Claim();

        var result = document.MarkFailed();

        Assert.True(result.IsSuccess);
        Assert.Equal(GeneratedDocumentStatus.Failed, document.Status);
        Assert.Null(document.StorageKey);
    }

    [Fact]
    public void MarkFailed_on_an_already_Ready_document_is_rejected()
    {
        var document = Claim();
        document.AttachUploadedArtifact("key/1.pdf", "sha256hash", "application/pdf", 1024);
        document.MarkReady(DateTimeOffset.UtcNow);

        var result = document.MarkFailed();

        Assert.True(result.IsFailure);
        Assert.Equal(GeneratedDocumentStatus.Ready, document.Status);
    }

    [Fact]
    public void Reopen_a_Failed_claim_allows_a_retry_without_a_new_row()
    {
        var document = Claim();
        document.MarkFailed();

        var result = document.Reopen();

        Assert.True(result.IsSuccess);
        Assert.Equal(GeneratedDocumentStatus.Pending, document.Status);
    }

    [Fact]
    public void Reopen_a_document_that_never_failed_is_rejected()
    {
        var document = Claim();

        var result = document.Reopen();

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Revoke_requires_Ready_status()
    {
        var document = Claim();

        var result = document.Revoke("no longer valid", DateTimeOffset.UtcNow);

        Assert.True(result.IsFailure);
        Assert.Equal(GeneratedDocumentStatus.Pending, document.Status);
    }

    [Fact]
    public void Revoke_a_Ready_document_succeeds_and_records_the_reason()
    {
        var document = Claim();
        document.AttachUploadedArtifact("key/1.pdf", "sha256hash", "application/pdf", 1024);
        document.MarkReady(DateTimeOffset.UtcNow);

        var result = document.Revoke("Issued to the wrong student", DateTimeOffset.UtcNow);

        Assert.True(result.IsSuccess);
        Assert.Equal(GeneratedDocumentStatus.Revoked, document.Status);
        Assert.Equal("Issued to the wrong student", document.RevokedReason);
        Assert.NotNull(document.RevokedAt);
    }

    [Fact]
    public void Revoke_requires_a_non_empty_reason()
    {
        var document = Claim();
        document.AttachUploadedArtifact("key/1.pdf", "sha256hash", "application/pdf", 1024);
        document.MarkReady(DateTimeOffset.UtcNow);

        var result = document.Revoke("   ", DateTimeOffset.UtcNow);

        Assert.True(result.IsFailure);
        Assert.Equal(GeneratedDocumentStatus.Ready, document.Status);
    }

    [Fact]
    public void Revoke_an_already_Revoked_document_is_rejected()
    {
        var document = Claim();
        document.AttachUploadedArtifact("key/1.pdf", "sha256hash", "application/pdf", 1024);
        document.MarkReady(DateTimeOffset.UtcNow);
        document.Revoke("first revoke", DateTimeOffset.UtcNow);

        var result = document.Revoke("second revoke", DateTimeOffset.UtcNow);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Supersede_requires_Ready_status()
    {
        var document = Claim();

        var result = document.Supersede(GeneratedDocumentId.New());

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Supersede_a_Ready_document_records_the_replacement_id()
    {
        var document = Claim();
        document.AttachUploadedArtifact("key/1.pdf", "sha256hash", "application/pdf", 1024);
        document.MarkReady(DateTimeOffset.UtcNow);
        var replacementId = GeneratedDocumentId.New();

        var result = document.Supersede(replacementId);

        Assert.True(result.IsSuccess);
        Assert.Equal(GeneratedDocumentStatus.Superseded, document.Status);
        Assert.Equal(replacementId, document.SupersededByDocumentId);
    }
}
