using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Finance.Domain.FeeStructures;

/// <summary>
/// requirement-spec.md finance §2's applicability concept. <see cref="ReferenceId"/> is deliberately
/// a plain <see cref="Guid"/>, never Organization's own <c>ProgramId</c> or Admission's own
/// <c>AdmissionCampaignId</c> - Finance depends on no other module's domain types
/// (module-boundaries.md: "Finance intentionally has no outgoing domain dependency"), so it can only
/// ever hold another module's id opaquely.
/// </summary>
public sealed record FeeApplicability
{
    private FeeApplicability(FeeApplicabilityType type, Guid? referenceId, string? serviceName)
    {
        Type = type;
        ReferenceId = referenceId;
        ServiceName = serviceName;
    }

    public FeeApplicabilityType Type { get; }

    public Guid? ReferenceId { get; }

    public string? ServiceName { get; }

    public static Result<FeeApplicability> ForProgram(Guid programId) =>
        programId == Guid.Empty
            ? Error.Validation("fee_applicability.program_id_required", "A Program applicability requires a non-empty programId.")
            : new FeeApplicability(FeeApplicabilityType.Program, programId, null);

    public static Result<FeeApplicability> ForAdmissionCampaign(Guid admissionCampaignId) =>
        admissionCampaignId == Guid.Empty
            ? Error.Validation("fee_applicability.admission_campaign_id_required", "An AdmissionCampaign applicability requires a non-empty admissionCampaignId.")
            : new FeeApplicability(FeeApplicabilityType.AdmissionCampaign, admissionCampaignId, null);

    public static Result<FeeApplicability> ForService(string serviceName) =>
        string.IsNullOrWhiteSpace(serviceName)
            ? Error.Validation("fee_applicability.service_name_required", "A Service applicability requires a non-empty serviceName.")
            : new FeeApplicability(FeeApplicabilityType.Service, null, serviceName.Trim());

    /// <summary>For infrastructure round-tripping of an already-validated stored value only.</summary>
    public static FeeApplicability FromStoredValue(FeeApplicabilityType type, Guid? referenceId, string? serviceName) => new(type, referenceId, serviceName);
}
