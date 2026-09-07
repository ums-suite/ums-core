namespace UMS.Modules.Admission.Application.Applications;

public sealed record ApplicationDto(
    Guid Id,
    Guid ApplicantId,
    Guid CampaignId,
    string Status,
    string? ApplicationNumber,
    IReadOnlyCollection<ProgramChoiceDto> ProgramChoices,
    IReadOnlyCollection<ApplicationDocumentDto> Documents,
    Guid? ApplicationFeeInvoiceId,
    bool IsApplicationFeePaid,
    Guid? ConfirmationFeeInvoiceId,
    bool IsConfirmationFeePaid,
    Guid? AssignedTestSlotId,
    string? RollNumber,
    Guid? AdmitCardDocumentId);

public sealed record ProgramChoiceDto(Guid ProgramId, int Rank);

public sealed record ApplicationDocumentDto(Guid Id, string DocumentType, string FileReference, string Status, string? RejectionReason);

public sealed record ProgramChoiceRequest(Guid ProgramId, int Rank);

public sealed record UploadDocumentRequest(string DocumentType, string FileReference);

/// <summary>ADM-20's combined "ensure a confirmation Invoice exists, complete confirmation once paid+verified" result - see <see cref="ApplicationService.ConfirmAsync"/>'s own remarks.</summary>
public sealed record ConfirmationAttemptResult(ApplicationDto Application, Guid? ConfirmationFeeInvoiceId, bool Confirmed);
