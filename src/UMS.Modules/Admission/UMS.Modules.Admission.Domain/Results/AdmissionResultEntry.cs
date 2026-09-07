using UMS.Modules.Admission.Domain.MeritLists;

namespace UMS.Modules.Admission.Domain.Results;

/// <summary>docs/ddd/ubiquitous-language.md: "the published, per-Applicant outcome (admitted/waitlisted/rejected) derived from a MeritList" - modeled here as an Entity owned by the per-Campaign <see cref="AdmissionResult"/> batch (see that aggregate's own remarks on this documented shape deviation).</summary>
public sealed class AdmissionResultEntry
{
    internal AdmissionResultEntry(Guid applicantId, Guid applicationId, Guid programId, MeritOutcome outcome, int meritRank, int? waitlistRank)
    {
        ApplicantId = applicantId;
        ApplicationId = applicationId;
        ProgramId = programId;
        Outcome = outcome;
        MeritRank = meritRank;
        WaitlistRank = waitlistRank;
    }

    private AdmissionResultEntry()
    {
    }

    public Guid ApplicantId { get; private set; }

    public Guid ApplicationId { get; private set; }

    public Guid ProgramId { get; private set; }

    public MeritOutcome Outcome { get; private set; }

    public int MeritRank { get; private set; }

    public int? WaitlistRank { get; private set; }

    internal void ApplyPromotion()
    {
        Outcome = MeritOutcome.Admitted;
        WaitlistRank = null;
    }
}
