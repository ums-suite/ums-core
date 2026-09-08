using UMS.Modules.Alumni.Domain.Mentorship;

namespace UMS.Modules.Alumni.UnitTests.Mentorship;

/// <summary>requirement-spec.md §4: "requires two-sided acceptance before becoming Active."</summary>
public sealed class MentorshipMatchTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Propose_starts_Proposed_with_neither_side_accepted()
    {
        var match = MentorshipMatch.Propose(Guid.NewGuid(), Guid.NewGuid(), Now);

        Assert.Equal(MentorshipMatchStatus.Proposed, match.Status);
        Assert.Null(match.MentorAcceptedAt);
        Assert.Null(match.MenteeAcceptedAt);
    }

    [Fact]
    public void Accepting_only_the_mentor_side_does_not_activate()
    {
        var match = MentorshipMatch.Propose(Guid.NewGuid(), Guid.NewGuid(), Now);

        match.AcceptByMentor(Now.AddHours(1));

        Assert.Equal(MentorshipMatchStatus.Proposed, match.Status);
        Assert.Empty(match.DomainEvents);
    }

    [Fact]
    public void Accepting_only_the_mentee_side_does_not_activate()
    {
        var match = MentorshipMatch.Propose(Guid.NewGuid(), Guid.NewGuid(), Now);

        match.AcceptByMentee(Now.AddHours(1));

        Assert.Equal(MentorshipMatchStatus.Proposed, match.Status);
        Assert.Empty(match.DomainEvents);
    }

    [Fact]
    public void Both_sides_accepting_activates_and_raises_MentorshipMatchAccepted_exactly_once()
    {
        var match = MentorshipMatch.Propose(Guid.NewGuid(), Guid.NewGuid(), Now);

        match.AcceptByMentor(Now.AddHours(1));
        match.AcceptByMentee(Now.AddHours(2));

        Assert.Equal(MentorshipMatchStatus.Active, match.Status);
        Assert.Single(match.DomainEvents);
    }

    [Fact]
    public void Accepting_twice_by_the_same_side_is_idempotent_and_does_not_double_activate()
    {
        var match = MentorshipMatch.Propose(Guid.NewGuid(), Guid.NewGuid(), Now);

        match.AcceptByMentor(Now.AddHours(1));
        match.AcceptByMentor(Now.AddHours(2));
        match.AcceptByMentee(Now.AddHours(3));

        Assert.Equal(MentorshipMatchStatus.Active, match.Status);
        Assert.Single(match.DomainEvents);
    }

    [Fact]
    public void End_requires_Active_status()
    {
        var match = MentorshipMatch.Propose(Guid.NewGuid(), Guid.NewGuid(), Now);

        Assert.Throws<InvalidOperationException>(() => match.End("withdrew", Now));
    }

    [Fact]
    public void End_on_an_Active_match_raises_MentorshipMatchEnded()
    {
        var match = MentorshipMatch.Propose(Guid.NewGuid(), Guid.NewGuid(), Now);
        match.AcceptByMentor(Now);
        match.AcceptByMentee(Now);

        match.End("mentor withdrew", Now.AddDays(10));

        Assert.Equal(MentorshipMatchStatus.Ended, match.Status);
        Assert.Contains(match.DomainEvents, e => e.GetType().Name == "MentorshipMatchEnded");
    }

    [Fact]
    public void Reject_only_valid_while_still_Proposed()
    {
        var match = MentorshipMatch.Propose(Guid.NewGuid(), Guid.NewGuid(), Now);
        match.AcceptByMentor(Now);
        match.AcceptByMentee(Now);

        Assert.Throws<InvalidOperationException>(() => match.Reject("no longer needed", Now));
    }
}
