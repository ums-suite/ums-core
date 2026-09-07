using UMS.Modules.Admission.Domain.Common;
using UMS.Modules.Admission.Domain.Events;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Admission.Domain.Applications;

/// <summary>
/// ADM-4..8: one Applicant's submission against one AdmissionCampaign (docs/ddd/ubiquitous-
/// language.md) - requirement-spec.md §4's "one Application per (Applicant, AdmissionCampaign)"
/// invariant, enforced by a DB-level unique index (ApplicationConfiguration), never re-derived here.
///
/// <para>
/// <b>Submit/lock concurrency mechanism - read this before changing <see cref="Lock"/>.</b>
/// design-decisions.md "Idempotency for Application Submission": the transition off `Draft` is
/// guarded by a state-guarded conditional <c>UPDATE ... WHERE status = 'Draft'</c>
/// (<c>ApplicationRepository.TryLockAsync</c>, mirroring <c>ResultPublicationRepository.TryTransitionAsync</c>'s
/// exact shape), issued identically by the applicant-facing submit endpoint AND the Finance-
/// payment-confirmation relay (edge-cases.md "Duplicate payment webhook racing the application-lock
/// transaction"). <see cref="Lock"/> itself only validates the in-memory precondition and mutates
/// fields for the CALLER that already won that race - it is not, on its own, what makes a second
/// concurrent caller's transition a no-op; that guarantee lives entirely in the repository's atomic
/// `WHERE` clause.
/// </para>
/// </summary>
public sealed class Application : AggregateRoot<ApplicationId>
{
    private readonly List<ProgramChoice> _programChoices = [];
    private readonly List<ApplicationDocument> _documents = [];

    private Application()
    {
    }

    private Application(ApplicationId id, Guid applicantId, Guid campaignId, DateTimeOffset now)
    {
        Id = id;
        ApplicantId = applicantId;
        CampaignId = campaignId;
        Status = ApplicationStatus.Draft;
        CreatedAt = now;
    }

    public Guid ApplicantId { get; private set; }

    public Guid CampaignId { get; private set; }

    public IReadOnlyCollection<ProgramChoice> ProgramChoices => _programChoices.AsReadOnly();

    public IReadOnlyCollection<ApplicationDocument> Documents => _documents.AsReadOnly();

    public ApplicationStatus Status { get; private set; }

    /// <summary>Assigned exactly once, at the same instant as <see cref="Lock"/> (requirement-spec.md §2: "an application number is generated").</summary>
    public string? ApplicationNumber { get; private set; }

    public Guid? ApplicationFeeInvoiceId { get; private set; }

    public bool IsApplicationFeePaid { get; private set; }

    public Guid? ConfirmationFeeInvoiceId { get; private set; }

    public bool IsConfirmationFeePaid { get; private set; }

    /// <summary>ADM-9: assigned once, immediately after <see cref="Lock"/>, by the atomic test-slot-capacity-claim mechanism (edge-cases.md "Test-slot capacity race at admit-card generation").</summary>
    public Guid? AssignedTestSlotId { get; private set; }

    public string? RollNumber { get; private set; }

    public Guid? AdmitCardDocumentId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? SubmittedAt { get; private set; }

    public DateTimeOffset? LockedAt { get; private set; }

    public DateTimeOffset? ConfirmedAt { get; private set; }

    public static Result<Application> CreateDraft(Guid applicantId, Guid campaignId, DateTimeOffset now)
    {
        if (applicantId == Guid.Empty || campaignId == Guid.Empty)
        {
            return Error.Validation("application.identifiers_required", "An Application requires both a valid applicantId and campaignId.");
        }

        return new Application(ApplicationId.New(), applicantId, campaignId, now);
    }

    /// <summary>Only while <see cref="ApplicationStatus.Draft"/> (requirement-spec.md §2: "a locked Application's ProgramChoices ... become immutable").</summary>
    public Result ReplaceProgramChoices(IReadOnlyCollection<ProgramChoice> choices)
    {
        var mutability = EnsureDraft();
        if (mutability.IsFailure)
        {
            return mutability;
        }

        if (choices is not { Count: > 0 })
        {
            return Result.Failure(Error.Validation("application.program_choices_required", "At least one ProgramChoice is required."));
        }

        if (choices.Select(c => c.ProgramId).Distinct().Count() != choices.Count)
        {
            return Result.Failure(Error.Validation("application.duplicate_program_choice", "A Program may only appear once among an Application's ProgramChoices."));
        }

        _programChoices.Clear();
        _programChoices.AddRange(choices.OrderBy(c => c.Rank));
        return Result.Success();
    }

    /// <summary>Uploads/replaces one document by type. A rejected document may be re-uploaded even once <see cref="ApplicationStatus.Locked"/> (§8 edge case) - every other type requires <see cref="ApplicationStatus.Draft"/>.</summary>
    public Result AddOrReplaceDocument(string documentType, string fileReference, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(documentType) || string.IsNullOrWhiteSpace(fileReference))
        {
            return Result.Failure(Error.Validation("application.document_fields_required", "A document's type and file reference are both required."));
        }

        var existing = _documents.FirstOrDefault(d => string.Equals(d.DocumentType, documentType, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            var mutability = EnsureDraft();
            if (mutability.IsFailure)
            {
                return mutability;
            }

            _documents.Add(new ApplicationDocument(Guid.NewGuid(), documentType.Trim(), fileReference.Trim(), now));
            return Result.Success();
        }

        if (Status == ApplicationStatus.Draft || existing.Status == ApplicationDocumentStatus.Rejected)
        {
            existing.Replace(fileReference.Trim(), now);
            return Result.Success();
        }

        return Result.Failure(Error.Conflict("application.document_not_reopenable", $"Document '{documentType}' cannot be re-uploaded - the Application is Locked and this document has not been rejected for resubmission."));
    }

    public Result ApproveDocument(Guid documentId, Guid reviewedByUserId, DateTimeOffset now)
    {
        var document = _documents.FirstOrDefault(d => d.Id == documentId);
        if (document is null)
        {
            return Result.Failure(Error.NotFound("application.document_not_found", $"No document '{documentId}' exists on this Application."));
        }

        document.Approve(reviewedByUserId, now);
        return Result.Success();
    }

    /// <summary>§8 edge case: "Officer requests resubmission; the specific document reopens for re-upload without unlocking the rest of the Application."</summary>
    public Result RequestDocumentResubmission(Guid documentId, string reason, Guid reviewedByUserId, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return Result.Failure(Error.Validation("application.rejection_reason_required", "A resubmission request requires a reason."));
        }

        var document = _documents.FirstOrDefault(d => d.Id == documentId);
        if (document is null)
        {
            return Result.Failure(Error.NotFound("application.document_not_found", $"No document '{documentId}' exists on this Application."));
        }

        document.RequestResubmission(reason.Trim(), reviewedByUserId, now);
        return Result.Success();
    }

    /// <summary>ADM-7: recorded once Finance's CreateInvoice call for the application fee returns (requirement-spec.md §2/§7).</summary>
    public void RecordApplicationFeeInvoice(Guid invoiceId)
    {
        ApplicationFeeInvoiceId = invoiceId;
    }

    public void RecordConfirmationFeeInvoice(Guid invoiceId)
    {
        ConfirmationFeeInvoiceId = invoiceId;
    }

    /// <summary>ADM-9: called once the atomic slot-seat claim (repository level) already succeeded - idempotent-safe (a re-assignment attempt against an already-assigned Application is a no-op).</summary>
    public void AssignTestSlot(Guid testSlotId, string rollNumber)
    {
        if (AssignedTestSlotId is not null)
        {
            return;
        }

        AssignedTestSlotId = testSlotId;
        RollNumber = rollNumber;
    }

    public void RecordAdmitCard(Guid documentId)
    {
        AdmitCardDocumentId = documentId;
    }

    /// <summary>requirement-spec.md §4: gate met only by a webhook-verified Successful Payment (never the redirect return) - called from the Finance-payment-event relay described in this class's own remarks.</summary>
    public void MarkApplicationFeePaid()
    {
        IsApplicationFeePaid = true;
    }

    public void MarkConfirmationFeePaid()
    {
        IsConfirmationFeePaid = true;
    }

    /// <summary>
    /// requirement-spec.md §2/§4's submit gate, checked entirely in-memory BEFORE the repository's
    /// atomic conditional write is attempted (the write itself is what actually resolves a race
    /// between two callers - see class remarks) - this method is safe to call speculatively to
    /// produce a good validation error, and is also what <c>ApplicationService</c> calls again,
    /// against a freshly re-read aggregate, immediately before issuing that conditional write.
    /// </summary>
    public Result EnsureSubmittable(IReadOnlyCollection<string> requiredDocumentTypes)
    {
        if (Status != ApplicationStatus.Draft)
        {
            // §8 edge case: "a second submit call against an already-Locked application is a
            // no-op returning the existing state, not an error" - the CALLER (ApplicationService)
            // is responsible for turning this specific failure into that no-op replay, since this
            // method alone cannot return the existing state.
            return Result.Failure(Error.Conflict("application.not_draft", $"Application '{Id}' is not Draft (currently '{Status}')."));
        }

        if (_programChoices.Count == 0)
        {
            return Result.Failure(Error.Validation("application.no_program_choices", "At least one ProgramChoice is required before submitting."));
        }

        var missing = requiredDocumentTypes
            .Where(required => !_documents.Any(d => string.Equals(d.DocumentType, required, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        if (missing.Count > 0)
        {
            return Result.Failure(Error.Validation("application.documents_missing", $"Missing required document(s): {string.Join(", ", missing)}."));
        }

        if (!IsApplicationFeePaid)
        {
            return Result.Failure(Error.Conflict("application.fee_not_paid", "The application fee Payment has not yet been confirmed successful."));
        }

        return Result.Success();
    }

    /// <summary>
    /// Called only by <c>ApplicationService</c>/the payment-confirmation relay, immediately AFTER
    /// the repository's own conditional <c>WHERE status = 'Draft'</c> write already won the race for
    /// this specific call - see class remarks. Idempotent-safe to call redundantly since the
    /// repository never invokes it a second time for a losing caller.
    /// </summary>
    public void Lock(string applicationNumber, DateTimeOffset now)
    {
        Status = ApplicationStatus.Locked;
        ApplicationNumber = applicationNumber;
        SubmittedAt = now;
        LockedAt = now;
        Raise(new ApplicationSubmitted(Id.Value, ApplicantId, CampaignId, applicationNumber, now));
        Raise(new ApplicationLocked(Id.Value, now));
    }

    /// <summary>ADM-20/21: offer acceptance - the confirmation-fee Payment must already be confirmed Successful and every required document must be Approved (requirement-spec.md §2 Admission Confirmation → Handoff).</summary>
    public Result Confirm(DateTimeOffset now)
    {
        if (Status != ApplicationStatus.Locked)
        {
            return Result.Failure(Error.Conflict("application.not_locked", $"Application '{Id}' cannot be confirmed - it is currently '{Status}' (requires 'Locked')."));
        }

        if (!IsConfirmationFeePaid)
        {
            return Result.Failure(Error.Conflict("application.confirmation_fee_not_paid", "The admission-confirmation fee Payment has not yet been confirmed successful."));
        }

        var unapproved = _documents.Where(d => d.Status != ApplicationDocumentStatus.Approved).ToList();
        if (unapproved.Count > 0)
        {
            return Result.Failure(Error.Conflict("application.documents_not_verified", "Every document must be Approved before confirmation."));
        }

        Status = ApplicationStatus.Confirmed;
        ConfirmedAt = now;
        Raise(new AdmissionConfirmed(Id.Value, ApplicantId, CampaignId, now));
        return Result.Success();
    }

    /// <summary>ADM-22: a Locked, never-confirmed Application whose waitlist slot is being released back (requirement-spec.md §8 edge case, §9 decision 2 - a manual, audited admin action only, never automatic).</summary>
    public Result Decline(DateTimeOffset now)
    {
        if (Status != ApplicationStatus.Locked)
        {
            return Result.Failure(Error.Conflict("application.not_locked", $"Application '{Id}' cannot be declined - it is currently '{Status}' (requires 'Locked')."));
        }

        Status = ApplicationStatus.Declined;
        return Result.Success();
    }

    private Result EnsureDraft() =>
        Status == ApplicationStatus.Draft
            ? Result.Success()
            : Result.Failure(Error.Conflict("application.locked", $"Application '{Id}' is '{Status}' and can no longer be edited."));
}
