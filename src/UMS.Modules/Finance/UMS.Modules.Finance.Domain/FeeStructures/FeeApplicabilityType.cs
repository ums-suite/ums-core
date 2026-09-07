namespace UMS.Modules.Finance.Domain.FeeStructures;

/// <summary>requirement-spec.md finance §2: "applicability (Program / AdmissionCampaign / Service such as hostel or a library fine settlement)".</summary>
public enum FeeApplicabilityType
{
    Program,
    AdmissionCampaign,
    Service,
}
