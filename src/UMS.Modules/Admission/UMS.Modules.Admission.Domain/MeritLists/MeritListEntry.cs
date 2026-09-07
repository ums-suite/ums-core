namespace UMS.Modules.Admission.Domain.MeritLists;

/// <summary>One Applicant's ranked position within a <see cref="MeritList"/>, scoped to the single Program their evaluation was ranked against (see <see cref="MeritList.Generate"/>'s own remarks on the first-choice-only simplification).</summary>
public sealed class MeritListEntry
{
    internal MeritListEntry(Guid applicantId, Guid applicationId, Guid programId, decimal score, int rank, MeritOutcome outcome, int? waitlistRank)
    {
        ApplicantId = applicantId;
        ApplicationId = applicationId;
        ProgramId = programId;
        Score = score;
        Rank = rank;
        Outcome = outcome;
        WaitlistRank = waitlistRank;
    }

    private MeritListEntry()
    {
    }

    public Guid ApplicantId { get; private set; }

    public Guid ApplicationId { get; private set; }

    public Guid ProgramId { get; private set; }

    public decimal Score { get; private set; }

    public int Rank { get; private set; }

    public MeritOutcome Outcome { get; private set; }

    public int? WaitlistRank { get; private set; }

    internal void SetOutcome(MeritOutcome outcome, int? waitlistRank)
    {
        Outcome = outcome;
        WaitlistRank = waitlistRank;
    }
}
