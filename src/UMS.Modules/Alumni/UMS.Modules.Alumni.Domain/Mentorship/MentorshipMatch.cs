using UMS.Modules.Alumni.Domain.Common;
using UMS.Modules.Alumni.Domain.Events;

namespace UMS.Modules.Alumni.Domain.Mentorship;

/// <summary>
/// ALM-12/ALM-13: pairs exactly one Alumnus (mentor) with one Student (mentee) per match
/// (requirement-spec.md §2.5, §3, §4).
///
/// <para>
/// requirement-spec.md §4: "requires two-sided acceptance before becoming Active - a mentor cannot
/// be assigned a mentee (or vice versa) unilaterally." Tracked via <see cref="MentorAcceptedAt"/>/
/// <see cref="MenteeAcceptedAt"/> independently; the match becomes <see cref="MentorshipMatchStatus.Active"/>
/// the instant BOTH are set (an intermediate "Accepted-but-not-yet-Active" state is never separately
/// observable - the spec's own lifecycle table lists them as one forward step, and nothing in this
/// module needs to distinguish the two once both consents are recorded).
/// </para>
///
/// <para>
/// design-decisions.md "Mentor-Capacity Enforcement Mechanism": THIS class does not itself enforce
/// capacity - the atomic conditional write happens in the repository/application layer BEFORE a
/// <see cref="MentorshipMatch"/> is even constructed (a proposal is only created if that write
/// actually claimed a slot), and capacity is released again by the application layer calling the
/// same repository's decrement on <see cref="End"/>/<see cref="Reject"/>.
/// </para>
/// </summary>
public sealed class MentorshipMatch : AggregateRoot<MentorshipMatchId>
{
    private MentorshipMatch()
    {
    }

    private MentorshipMatch(MentorshipMatchId id, Guid mentorAlumnusId, Guid menteeStudentId, DateTimeOffset now)
    {
        Id = id;
        MentorAlumnusId = mentorAlumnusId;
        MenteeStudentId = menteeStudentId;
        Status = MentorshipMatchStatus.Proposed;
        ProposedAt = now;
    }

    public Guid MentorAlumnusId { get; private set; }

    public Guid MenteeStudentId { get; private set; }

    public MentorshipMatchStatus Status { get; private set; }

    public DateTimeOffset ProposedAt { get; private set; }

    public DateTimeOffset? MentorAcceptedAt { get; private set; }

    public DateTimeOffset? MenteeAcceptedAt { get; private set; }

    public DateTimeOffset? ActivatedAt { get; private set; }

    public DateTimeOffset? EndedAt { get; private set; }

    public string? EndedReason { get; private set; }

    public static MentorshipMatch Propose(Guid mentorAlumnusId, Guid menteeStudentId, DateTimeOffset now) =>
        new(MentorshipMatchId.New(), mentorAlumnusId, menteeStudentId, now);

    public void AcceptByMentor(DateTimeOffset now)
    {
        EnsureProposed();
        MentorAcceptedAt ??= now;
        ActivateIfBothAccepted(now);
    }

    public void AcceptByMentee(DateTimeOffset now)
    {
        EnsureProposed();
        MenteeAcceptedAt ??= now;
        ActivateIfBothAccepted(now);
    }

    /// <summary>A coordinator/Admin rejecting a still-Proposed match before both sides accept - releases the mentor's claimed capacity slot (handled by the calling application service).</summary>
    public void Reject(string reason, DateTimeOffset now)
    {
        EnsureProposed();
        Status = MentorshipMatchStatus.Ended;
        EndedAt = now;
        EndedReason = reason;
        Raise(new MentorshipMatchEnded(Id.Value, MentorAlumnusId, MenteeStudentId, reason, now));
    }

    /// <summary>edge-cases.md "A mentor withdraws mid-match": either side may end an Active match; reason recorded (requirement-spec.md §2.5 last bullet).</summary>
    public void End(string reason, DateTimeOffset now)
    {
        if (Status != MentorshipMatchStatus.Active)
        {
            throw new InvalidOperationException($"Cannot end a MentorshipMatch in status {Status} - only an Active match may be ended.");
        }

        Status = MentorshipMatchStatus.Ended;
        EndedAt = now;
        EndedReason = reason;
        Raise(new MentorshipMatchEnded(Id.Value, MentorAlumnusId, MenteeStudentId, reason, now));
    }

    public void Complete(DateTimeOffset now)
    {
        if (Status != MentorshipMatchStatus.Active)
        {
            throw new InvalidOperationException($"Cannot complete a MentorshipMatch in status {Status} - only an Active match may be completed.");
        }

        Status = MentorshipMatchStatus.Completed;
        EndedAt = now;
    }

    private void ActivateIfBothAccepted(DateTimeOffset now)
    {
        if (MentorAcceptedAt is not null && MenteeAcceptedAt is not null && Status == MentorshipMatchStatus.Proposed)
        {
            Status = MentorshipMatchStatus.Active;
            ActivatedAt = now;
            Raise(new MentorshipMatchAccepted(Id.Value, MentorAlumnusId, MenteeStudentId, now));
        }
    }

    private void EnsureProposed()
    {
        if (Status != MentorshipMatchStatus.Proposed)
        {
            throw new InvalidOperationException($"Cannot accept/reject a MentorshipMatch in status {Status} - only a Proposed match may be.");
        }
    }
}
