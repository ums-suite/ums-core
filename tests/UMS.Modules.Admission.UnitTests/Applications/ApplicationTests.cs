using UMS.Modules.Admission.Domain.Applications;
using AdmissionApplication = UMS.Modules.Admission.Domain.Applications.Application;

namespace UMS.Modules.Admission.UnitTests.Applications;

/// <summary>ADM-4..8/ADM-20/22: requirement-spec.md §2 Application Lifecycle, §4's invariants, §8's edge cases.</summary>
public sealed class ApplicationTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static AdmissionApplication Draft() => AdmissionApplication.CreateDraft(Guid.NewGuid(), Guid.NewGuid(), Now).Value;

    [Fact]
    public void Submitting_without_any_program_choice_is_rejected()
    {
        var application = Draft();

        var result = application.EnsureSubmittable([]);

        Assert.True(result.IsFailure);
        Assert.Equal("application.no_program_choices", result.Error!.Code);
    }

    [Fact]
    public void Submitting_with_a_missing_required_document_is_rejected()
    {
        var application = Draft();
        application.ReplaceProgramChoices([ProgramChoice.Create(Guid.NewGuid(), 1).Value]);

        var result = application.EnsureSubmittable(["TranscriptCopy"]);

        Assert.True(result.IsFailure);
        Assert.Equal("application.documents_missing", result.Error!.Code);
    }

    [Fact]
    public void Submitting_before_the_application_fee_is_paid_is_rejected()
    {
        var application = Draft();
        application.ReplaceProgramChoices([ProgramChoice.Create(Guid.NewGuid(), 1).Value]);
        application.AddOrReplaceDocument("TranscriptCopy", "https://files.internal/t.pdf", Now);

        var result = application.EnsureSubmittable(["TranscriptCopy"]);

        Assert.True(result.IsFailure);
        Assert.Equal("application.fee_not_paid", result.Error!.Code);
    }

    [Fact]
    public void A_fully_satisfied_gate_allows_submission()
    {
        var application = Draft();
        application.ReplaceProgramChoices([ProgramChoice.Create(Guid.NewGuid(), 1).Value]);
        application.AddOrReplaceDocument("TranscriptCopy", "https://files.internal/t.pdf", Now);
        application.MarkApplicationFeePaid();

        var result = application.EnsureSubmittable(["TranscriptCopy"]);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Locking_assigns_the_application_number_and_raises_events()
    {
        var application = Draft();

        application.Lock("APP-2026-000001", Now);

        Assert.Equal(ApplicationStatus.Locked, application.Status);
        Assert.Equal("APP-2026-000001", application.ApplicationNumber);
        Assert.Equal(2, application.DomainEvents.Count);
    }

    [Fact]
    public void A_rejected_document_may_be_reuploaded_even_once_locked()
    {
        var application = Draft();
        application.AddOrReplaceDocument("TranscriptCopy", "https://files.internal/t.pdf", Now);
        application.Lock("APP-2026-000001", Now);
        var documentId = application.Documents.Single().Id;
        application.RequestDocumentResubmission(documentId, "blurry scan", Guid.NewGuid(), Now);

        var result = application.AddOrReplaceDocument("TranscriptCopy", "https://files.internal/t2.pdf", Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(ApplicationDocumentStatus.Pending, application.Documents.Single().Status);
    }

    [Fact]
    public void A_non_rejected_document_cannot_be_reuploaded_once_locked()
    {
        var application = Draft();
        application.AddOrReplaceDocument("TranscriptCopy", "https://files.internal/t.pdf", Now);
        application.Lock("APP-2026-000001", Now);

        var result = application.AddOrReplaceDocument("TranscriptCopy", "https://files.internal/t2.pdf", Now);

        Assert.True(result.IsFailure);
        Assert.Equal("application.document_not_reopenable", result.Error!.Code);
    }

    [Fact]
    public void Confirming_requires_the_confirmation_fee_paid_and_every_document_approved()
    {
        var application = Draft();
        application.AddOrReplaceDocument("TranscriptCopy", "https://files.internal/t.pdf", Now);
        application.Lock("APP-2026-000001", Now);

        var unpaid = application.Confirm(Now);
        Assert.True(unpaid.IsFailure);
        Assert.Equal("application.confirmation_fee_not_paid", unpaid.Error!.Code);

        application.MarkConfirmationFeePaid();
        var unapproved = application.Confirm(Now);
        Assert.True(unapproved.IsFailure);
        Assert.Equal("application.documents_not_verified", unapproved.Error!.Code);

        application.ApproveDocument(application.Documents.Single().Id, Guid.NewGuid(), Now);
        var confirmed = application.Confirm(Now);
        Assert.True(confirmed.IsSuccess);
        Assert.Equal(ApplicationStatus.Confirmed, application.Status);
    }

    [Fact]
    public void Declining_requires_the_application_to_be_locked()
    {
        var application = Draft();

        var result = application.Decline(Now);

        Assert.True(result.IsFailure);
        Assert.Equal("application.not_locked", result.Error!.Code);
    }
}
