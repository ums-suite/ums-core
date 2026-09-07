using UMS.Modules.Admission.Domain.Applications;
using AdmissionApplication = UMS.Modules.Admission.Domain.Applications.Application;
using ApplicationId = UMS.Modules.Admission.Domain.Applications.ApplicationId;

namespace UMS.Modules.Admission.Application.Abstractions;

public interface IApplicationRepository
{
    public Task<AdmissionApplication?> GetByIdAsync(ApplicationId id, CancellationToken cancellationToken = default);

    /// <summary>requirement-spec.md §4: "one Application per Applicant per AdmissionCampaign" - the natural key this checks (DB-level unique index backs it too, InvoiceIsRequired ApplicationConfiguration).</summary>
    public Task<AdmissionApplication?> GetByApplicantAndCampaignAsync(Guid applicantId, Guid campaignId, CancellationToken cancellationToken = default);

    public Task<AdmissionApplication?> GetByApplicationFeeInvoiceIdAsync(Guid invoiceId, CancellationToken cancellationToken = default);

    public Task<AdmissionApplication?> GetByConfirmationFeeInvoiceIdAsync(Guid invoiceId, CancellationToken cancellationToken = default);

    /// <summary>ADM-15: every Locked Application for a campaign - the candidate pool MeritList generation scans.</summary>
    public Task<IReadOnlyList<AdmissionApplication>> GetLockedByCampaignAsync(Guid campaignId, CancellationToken cancellationToken = default);

    public void Add(AdmissionApplication application);

    /// <summary>design-decisions.md "Idempotency for Application Submission": the state-guarded conditional <c>UPDATE ... WHERE status = 'Draft'</c>, shared by the applicant-facing submit endpoint and the Finance-payment-confirmation relay alike.</summary>
    public Task<bool> TryLockAsync(ApplicationId id, string applicationNumber, DateTimeOffset now, CancellationToken cancellationToken = default);

    /// <summary>edge-cases.md "Duplicate payment webhook": idempotent - `WHERE is_application_fee_paid = false` so a duplicate delivery is a silent no-op.</summary>
    public Task<bool> TryMarkApplicationFeePaidAsync(Guid invoiceId, CancellationToken cancellationToken = default);

    public Task<bool> TryMarkConfirmationFeePaidAsync(Guid invoiceId, CancellationToken cancellationToken = default);

    /// <summary>requirement-spec.md §9: a single, permanently-unique, monotonically increasing application number - backed by a static Postgres <c>SEQUENCE</c> created in the initial migration (never a dynamically-created per-campaign sequence).</summary>
    public Task<long> NextApplicationNumberSequenceValueAsync(CancellationToken cancellationToken = default);
}
