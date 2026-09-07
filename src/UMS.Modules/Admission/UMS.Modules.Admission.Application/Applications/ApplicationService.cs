using UMS.Modules.Admission.Application.Abstractions;
using UMS.Modules.Admission.Application.Campaigns;
using UMS.Modules.Admission.Application.Common;
using UMS.Modules.Admission.Domain.Applicants;
using UMS.Modules.Admission.Domain.Applications;
using UMS.Modules.Admission.Domain.Campaigns;
using UMS.Modules.Admission.Domain.Events;
using UMS.Shared.Audit;
using UMS.Shared.ErrorHandling.Results;
using UMS.Shared.Finance;
using UMS.Shared.Student;
using ApplicationId = UMS.Modules.Admission.Domain.Applications.ApplicationId;

namespace UMS.Modules.Admission.Application.Applications;

/// <summary>
/// ADM-4..8/ADM-20/21/22: the full Application lifecycle (requirement-spec.md §2 Application
/// Lifecycle, §2 Admission Confirmation → Handoff). See <see cref="Domain.Applications.Application"/>'s
/// own remarks for the submit/lock concurrency mechanism this service's <see cref="SubmitAsync"/>
/// relies on - identical to the mechanism <c>ApplicationPaymentConfirmationService</c> uses from the
/// Finance-payment-event relay side.
/// </summary>
public sealed class ApplicationService(
    IApplicationRepository applications,
    IApplicantRepository applicants,
    ICampaignRepository campaigns,
    IInvoiceRequester invoiceRequester,
    IStudentRecordProvisioner studentRecordProvisioner,
    AdmitCardService admitCardService,
    IUnitOfWork unitOfWork,
    IDomainEventRecorder domainEvents,
    IAuditRecorder auditRecorder,
    IClock clock)
{
    public async Task<Result<ApplicationDto>> CreateDraftAsync(Guid applicantId, Guid campaignId, CancellationToken cancellationToken = default)
    {
        var applicant = await applicants.GetByIdAsync(new ApplicantId(applicantId), cancellationToken).ConfigureAwait(false);
        if (applicant is null)
        {
            return Error.NotFound("application.applicant_not_found", $"No Applicant exists with id '{applicantId}'.");
        }

        if (!applicant.IsVerified)
        {
            return Error.Conflict("application.applicant_not_verified", "The Applicant must complete mobile or email OTP verification before creating an Application.");
        }

        var campaign = await campaigns.GetByIdAsync(new AdmissionCampaignId(campaignId), cancellationToken).ConfigureAwait(false);
        if (campaign is null)
        {
            return Error.NotFound("application.campaign_not_found", $"No AdmissionCampaign exists with id '{campaignId}'.");
        }

        var existing = await applications.GetByApplicantAndCampaignAsync(applicantId, campaignId, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return Error.Conflict("application.already_exists", $"Applicant '{applicantId}' already has an Application for Campaign '{campaignId}'.");
        }

        var created = Domain.Applications.Application.CreateDraft(applicantId, campaignId, clock.UtcNow);
        if (created.IsFailure)
        {
            return created.Error!;
        }

        applications.Add(created.Value);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DuplicateValueException)
        {
            // edge-cases.md-style genuinely concurrent double-create race against the same natural key.
            var winner = await applications.GetByApplicantAndCampaignAsync(applicantId, campaignId, cancellationToken).ConfigureAwait(false);
            return winner is null
                ? Error.Failure("application.creation_race_unresolved", "A concurrent Application creation conflict could not be resolved.")
                : ToDto(winner);
        }

        return ToDto(created.Value);
    }

    public async Task<Result<ApplicationDto>> ReplaceProgramChoicesAsync(Guid applicationId, IReadOnlyCollection<ProgramChoiceRequest> choices, CancellationToken cancellationToken = default)
    {
        var application = await applications.GetByIdAsync(new ApplicationId(applicationId), cancellationToken).ConfigureAwait(false);
        if (application is null)
        {
            return Error.NotFound("application.not_found", $"No Application exists with id '{applicationId}'.");
        }

        var built = new List<ProgramChoice>();
        foreach (var choice in choices)
        {
            var created = ProgramChoice.Create(choice.ProgramId, choice.Rank);
            if (created.IsFailure)
            {
                return created.Error!;
            }

            built.Add(created.Value);
        }

        var replaced = application.ReplaceProgramChoices(built);
        if (replaced.IsFailure)
        {
            return replaced.Error!;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDto(application);
    }

    public async Task<Result<ApplicationDto>> UploadDocumentAsync(Guid applicationId, UploadDocumentRequest request, CancellationToken cancellationToken = default)
    {
        var application = await applications.GetByIdAsync(new ApplicationId(applicationId), cancellationToken).ConfigureAwait(false);
        if (application is null)
        {
            return Error.NotFound("application.not_found", $"No Application exists with id '{applicationId}'.");
        }

        var added = application.AddOrReplaceDocument(request.DocumentType, request.FileReference, clock.UtcNow);
        if (added.IsFailure)
        {
            return added.Error!;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDto(application);
    }

    public async Task<Result> ApproveDocumentAsync(Guid applicationId, Guid documentId, Guid reviewerUserId, CancellationToken cancellationToken = default)
    {
        var application = await applications.GetByIdAsync(new ApplicationId(applicationId), cancellationToken).ConfigureAwait(false);
        if (application is null)
        {
            return Result.Failure(Error.NotFound("application.not_found", $"No Application exists with id '{applicationId}'."));
        }

        var approved = application.ApproveDocument(documentId, reviewerUserId, clock.UtcNow);
        if (approved.IsFailure)
        {
            return approved;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    /// <summary>§8 edge case: reopens only the specific document, never unlocking the rest of an already-Locked Application.</summary>
    public async Task<Result> RequestDocumentResubmissionAsync(Guid applicationId, Guid documentId, string reason, Guid reviewerUserId, CancellationToken cancellationToken = default)
    {
        var application = await applications.GetByIdAsync(new ApplicationId(applicationId), cancellationToken).ConfigureAwait(false);
        if (application is null)
        {
            return Result.Failure(Error.NotFound("application.not_found", $"No Application exists with id '{applicationId}'."));
        }

        var requested = application.RequestDocumentResubmission(documentId, reason, reviewerUserId, clock.UtcNow);
        if (requested.IsFailure)
        {
            return requested;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    /// <summary>ADM-7: idempotent by Finance's own natural-key dedupe (sourceModule/sourceReferenceId/feeType) - a retried call always returns the same Invoice.</summary>
    public async Task<Result<InvoiceSummary>> InitiateApplicationFeePaymentAsync(Guid applicationId, Guid requestedByUserId, string correlationId, CancellationToken cancellationToken = default)
    {
        var application = await applications.GetByIdAsync(new ApplicationId(applicationId), cancellationToken).ConfigureAwait(false);
        if (application is null)
        {
            return Error.NotFound("application.not_found", $"No Application exists with id '{applicationId}'.");
        }

        var applicant = await applicants.GetByIdAsync(new ApplicantId(application.ApplicantId), cancellationToken).ConfigureAwait(false);
        var campaign = await campaigns.GetByIdAsync(new AdmissionCampaignId(application.CampaignId), cancellationToken).ConfigureAwait(false);
        if (applicant is null || campaign is null)
        {
            return Error.Failure("application.invariant_violated", "The Application's own Applicant/Campaign could not be found.");
        }

        var invoice = await invoiceRequester.CreateInvoiceAsync(
            new CreateInvoiceCommand("admission", applicationId.ToString(), campaign.ApplicationFeeType, applicant.IdentityUserId, campaign.Id.Value, requestedByUserId, correlationId),
            cancellationToken).ConfigureAwait(false);

        if (invoice.IsFailure)
        {
            return invoice.Error!;
        }

        application.RecordApplicationFeeInvoice(invoice.Value.InvoiceId);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return invoice.Value;
    }

    /// <summary>
    /// ADM-8: the applicant-facing submit endpoint. Re-validates the gate against a fresh read, then
    /// issues the SAME state-guarded conditional write <c>ApplicationPaymentConfirmationService</c>
    /// uses (see <see cref="Domain.Applications.Application"/>'s own remarks) - a losing/already-
    /// Locked caller replays the current state rather than erroring (§8 edge case).
    /// </summary>
    public async Task<Result<ApplicationDto>> SubmitAsync(Guid applicationId, string correlationId, CancellationToken cancellationToken = default)
    {
        var application = await applications.GetByIdAsync(new ApplicationId(applicationId), cancellationToken).ConfigureAwait(false);
        if (application is null)
        {
            return Error.NotFound("application.not_found", $"No Application exists with id '{applicationId}'.");
        }

        if (application.Status != ApplicationStatus.Draft)
        {
            // §8 edge case: a second submit call against an already-Locked Application is a no-op
            // returning the existing state.
            return ToDto(application);
        }

        var campaign = await campaigns.GetByIdAsync(new AdmissionCampaignId(application.CampaignId), cancellationToken).ConfigureAwait(false);
        if (campaign is null)
        {
            return Error.Failure("application.invariant_violated", "The Application's own Campaign could not be found.");
        }

        var submittable = application.EnsureSubmittable(campaign.RequiredDocumentTypes);
        if (submittable.IsFailure)
        {
            return submittable.Error!;
        }

        return await TryLockAndAuditAsync(application, campaign, correlationId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>ADM-20: idempotent "attempt confirm" - ensures a confirmation-fee Invoice exists (creating it on first call), then completes confirmation + the Student handoff once it is paid and every document is Approved. See the ticket's own combined scope note in <c>ApplicationDto</c>'s remarks.</summary>
    public async Task<Result<ConfirmationAttemptResult>> ConfirmAsync(Guid applicationId, Guid requestedByUserId, string correlationId, CancellationToken cancellationToken = default)
    {
        var application = await applications.GetByIdAsync(new ApplicationId(applicationId), cancellationToken).ConfigureAwait(false);
        if (application is null)
        {
            return Error.NotFound("application.not_found", $"No Application exists with id '{applicationId}'.");
        }

        if (application.Status == ApplicationStatus.Confirmed)
        {
            return new ConfirmationAttemptResult(ToDto(application), application.ConfirmationFeeInvoiceId, Confirmed: true);
        }

        if (application.Status != ApplicationStatus.Locked)
        {
            return Error.Conflict("application.not_locked", $"Application '{applicationId}' cannot be confirmed - it is currently '{application.Status}' (requires 'Locked').");
        }

        if (application.ConfirmationFeeInvoiceId is null)
        {
            var applicant = await applicants.GetByIdAsync(new ApplicantId(application.ApplicantId), cancellationToken).ConfigureAwait(false);
            var campaign = await campaigns.GetByIdAsync(new AdmissionCampaignId(application.CampaignId), cancellationToken).ConfigureAwait(false);
            if (applicant is null || campaign is null)
            {
                return Error.Failure("application.invariant_violated", "The Application's own Applicant/Campaign could not be found.");
            }

            var invoice = await invoiceRequester.CreateInvoiceAsync(
                new CreateInvoiceCommand("admission", $"{applicationId}:confirmation", campaign.ConfirmationFeeType, applicant.IdentityUserId, campaign.Id.Value, requestedByUserId, correlationId),
                cancellationToken).ConfigureAwait(false);

            if (invoice.IsFailure)
            {
                return invoice.Error!;
            }

            application.RecordConfirmationFeeInvoice(invoice.Value.InvoiceId);
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        var confirmed = application.Confirm(clock.UtcNow);
        if (confirmed.IsFailure)
        {
            // Not yet payable/verifiable - the invoice now exists (or already did); report pending.
            return new ConfirmationAttemptResult(ToDto(application), application.ConfirmationFeeInvoiceId, Confirmed: false);
        }

        var auditRequest = AuditContext.ForSystemJob("application-confirm", correlationId, "Application", application.Id.Value.ToString(), AuditActions.Update, "{\"status\":\"Locked\"}", "{\"status\":\"Confirmed\"}");
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var committed = await TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, auditRequest, cancellationToken).ConfigureAwait(false);
        if (committed.IsFailure)
        {
            return committed.Error!;
        }

        // requirement-spec.md §2: "Admission calls Student's CreateStudentRecord command directly ...
        // Admission does NOT generate the student number itself" - idempotent by OriginatingApplicationId.
        var applicantForHandoff = await applicants.GetByIdAsync(new ApplicantId(application.ApplicantId), cancellationToken).ConfigureAwait(false);
        var campaignForHandoff = await campaigns.GetByIdAsync(new AdmissionCampaignId(application.CampaignId), cancellationToken).ConfigureAwait(false);

        // Known gap, documented rather than glossed over: Admission tracks only a chosen ProgramId
        // per ProgramChoice, never a resolved Department/faculty-code - a real deployment would
        // resolve these via Organization's own ancestor-hierarchy query (IOrganizationNodeExistenceChecker's
        // sibling contracts) before this handoff call. This build passes the Program id itself as a
        // stand-in DepartmentId and a fixed "GEN" FacultyCode, which Student's own permissive
        // IProgramExistenceChecker stub accepts unvalidated (the same stub-then-promote posture
        // Student's own requirement-spec §9 already documents for this exact field).
        var choice = application.ProgramChoices.OrderBy(c => c.Rank).First();
        _ = await studentRecordProvisioner.CreateAsync(
            new CreateStudentRecordCommand(
                application.Id.Value,
                clock.UtcNow.Year,
                FacultyCode: "GEN",
                DepartmentId: choice.ProgramId,
                ProgramId: choice.ProgramId,
                applicantForHandoff!.GivenName,
                applicantForHandoff.FamilyName,
                GivenNameBn: null,
                FamilyNameBn: null,
                applicantForHandoff.Email,
                applicantForHandoff.Mobile,
                applicantForHandoff.DateOfBirth,
                NationalId: null),
            cancellationToken).ConfigureAwait(false);
        _ = campaignForHandoff;

        return new ConfirmationAttemptResult(ToDto(application), application.ConfirmationFeeInvoiceId, Confirmed: true);
    }

    /// <summary>ADM-22: a manual, audited admin action - never automatic (requirement-spec.md §9 decision 2).</summary>
    public async Task<Result> DeclineAsync(Guid applicationId, Guid actingUserId, string correlationId, CancellationToken cancellationToken = default)
    {
        var application = await applications.GetByIdAsync(new ApplicationId(applicationId), cancellationToken).ConfigureAwait(false);
        if (application is null)
        {
            return Result.Failure(Error.NotFound("application.not_found", $"No Application exists with id '{applicationId}'."));
        }

        var declined = application.Decline(clock.UtcNow);
        if (declined.IsFailure)
        {
            return declined;
        }

        var auditRequest = new AuditContext(actingUserId, ActorIpAddress: null, correlationId)
            .ToRequest("Application", application.Id.Value.ToString(), AuditActions.Update, "{\"status\":\"Locked\"}", "{\"status\":\"Declined\"}");
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        return await TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, auditRequest, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<ApplicationDto>> GetByIdAsync(Guid applicationId, CancellationToken cancellationToken = default)
    {
        var application = await applications.GetByIdAsync(new ApplicationId(applicationId), cancellationToken).ConfigureAwait(false);
        return application is null
            ? Error.NotFound("application.not_found", $"No Application exists with id '{applicationId}'.")
            : ToDto(application);
    }

    public async Task<Result<ApplicationDto>> GetByApplicantAndCampaignAsync(Guid applicantId, Guid campaignId, CancellationToken cancellationToken = default)
    {
        var application = await applications.GetByApplicantAndCampaignAsync(applicantId, campaignId, cancellationToken).ConfigureAwait(false);
        return application is null
            ? Error.NotFound("application.not_found", $"No Application exists for Applicant '{applicantId}' / Campaign '{campaignId}'.")
            : ToDto(application);
    }

    internal static ApplicationDto ToDto(Domain.Applications.Application application) => new(
        application.Id.Value,
        application.ApplicantId,
        application.CampaignId,
        application.Status.ToString(),
        application.ApplicationNumber,
        application.ProgramChoices.Select(c => new ProgramChoiceDto(c.ProgramId, c.Rank)).ToList(),
        application.Documents.Select(d => new ApplicationDocumentDto(d.Id, d.DocumentType, d.FileReference, d.Status.ToString(), d.RejectionReason)).ToList(),
        application.ApplicationFeeInvoiceId,
        application.IsApplicationFeePaid,
        application.ConfirmationFeeInvoiceId,
        application.IsConfirmationFeePaid,
        application.AssignedTestSlotId,
        application.RollNumber,
        application.AdmitCardDocumentId);

    internal async Task<Result<ApplicationDto>> TryLockAndAuditAsync(Domain.Applications.Application application, AdmissionCampaign campaign, string correlationId, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var sequenceValue = await applications.NextApplicationNumberSequenceValueAsync(cancellationToken).ConfigureAwait(false);
        var applicationNumber = $"APP-{now.Year}-{sequenceValue:D6}";

        var locked = await applications.TryLockAsync(application.Id, applicationNumber, now, cancellationToken).ConfigureAwait(false);
        if (!locked)
        {
            // Lost the race to a concurrent caller (edge-cases.md duplicate-submit family) - replay
            // whatever the winner committed.
            var current = await applications.GetByIdAsync(application.Id, cancellationToken).ConfigureAwait(false);
            return current is null ? Error.Failure("application.not_found", "Application vanished mid-submit.") : ToDto(current);
        }

        campaign.LockConfiguration();

        domainEvents.Enqueue(new ApplicationSubmitted(application.Id.Value, application.ApplicantId, application.CampaignId, applicationNumber, now));
        domainEvents.Enqueue(new ApplicationLocked(application.Id.Value, now));

        var auditRequest = AuditContext.ForSystemJob("application-submit", correlationId, "Application", application.Id.Value.ToString(), AuditActions.Update, null, $"{{\"status\":\"Locked\",\"applicationNumber\":\"{applicationNumber}\"}}");
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var committed = await TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, auditRequest, cancellationToken).ConfigureAwait(false);
        if (committed.IsFailure)
        {
            return committed.Error!;
        }

        // ADM-9: best-effort, after the Application's own Locked transition already committed -
        // never re-opens or fails the submit itself (AdmitCardService's own remarks).
        await admitCardService.AssignAndGenerateAsync(application.Id.Value, application.CampaignId, correlationId, cancellationToken).ConfigureAwait(false);

        var reloaded = await applications.GetByIdAsync(application.Id, cancellationToken).ConfigureAwait(false);
        return reloaded is null ? Error.Failure("application.not_found", "Application vanished after submit.") : ToDto(reloaded);
    }
}
