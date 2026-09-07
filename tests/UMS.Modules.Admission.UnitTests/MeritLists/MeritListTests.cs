using UMS.Modules.Admission.Domain.MeritLists;

namespace UMS.Modules.Admission.UnitTests.MeritLists;

/// <summary>ADM-15/16/22: requirement-spec.md §4's seat-quota-bound invariant, §9 decision 2's manual-promotion posture.</summary>
public sealed class MeritListTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Ranking_never_admits_more_than_the_seat_quota()
    {
        var campaignId = Guid.NewGuid();
        var programId = Guid.NewGuid();
        var candidates = Enumerable.Range(0, 5)
            .Select(i => new MeritCandidate(Guid.NewGuid(), Guid.NewGuid(), programId, 100 - i))
            .ToList();

        var meritList = MeritList.Generate(campaignId, candidates, new Dictionary<Guid, int> { [programId] = 2 }, Now).Value;

        Assert.Equal(2, meritList.Entries.Count(e => e.Outcome == MeritOutcome.Admitted));
        Assert.Equal(3, meritList.Entries.Count(e => e.Outcome == MeritOutcome.Waitlisted));
        Assert.All(meritList.Entries.Where(e => e.Outcome == MeritOutcome.Admitted), e => Assert.Null(e.WaitlistRank));
        Assert.All(meritList.Entries.Where(e => e.Outcome == MeritOutcome.Waitlisted), e => Assert.NotNull(e.WaitlistRank));
    }

    [Fact]
    public void Higher_scores_rank_above_lower_scores()
    {
        var campaignId = Guid.NewGuid();
        var programId = Guid.NewGuid();
        var strongApplicant = Guid.NewGuid();
        var weakApplicant = Guid.NewGuid();
        var candidates = new[]
        {
            new MeritCandidate(weakApplicant, Guid.NewGuid(), programId, 40),
            new MeritCandidate(strongApplicant, Guid.NewGuid(), programId, 90),
        };

        var meritList = MeritList.Generate(campaignId, candidates, new Dictionary<Guid, int> { [programId] = 1 }, Now).Value;

        Assert.Equal(1, meritList.Entries.Single(e => e.ApplicantId == strongApplicant).Rank);
        Assert.Equal(MeritOutcome.Waitlisted, meritList.Entries.Single(e => e.ApplicantId == weakApplicant).Outcome);
    }

    [Fact]
    public void Approving_a_non_draft_merit_list_is_rejected()
    {
        var meritList = MeritList.Generate(Guid.NewGuid(), [], new Dictionary<Guid, int>(), Now).Value;
        meritList.Approve(Guid.NewGuid(), Now);

        var result = meritList.Approve(Guid.NewGuid(), Now);

        Assert.True(result.IsFailure);
        Assert.Equal("merit_list.not_draft", result.Error!.Code);
    }

    [Fact]
    public void Promoting_an_already_admitted_entry_is_rejected()
    {
        var programId = Guid.NewGuid();
        var applicantId = Guid.NewGuid();
        var meritList = MeritList.Generate(Guid.NewGuid(), [new MeritCandidate(applicantId, Guid.NewGuid(), programId, 90)], new Dictionary<Guid, int> { [programId] = 1 }, Now).Value;

        var result = meritList.PromoteWaitlisted(applicantId, programId);

        Assert.True(result.IsFailure);
        Assert.Equal("merit_list.not_waitlisted", result.Error!.Code);
    }

    [Fact]
    public void Promoting_a_waitlisted_entry_admits_it()
    {
        var programId = Guid.NewGuid();
        var admitted = Guid.NewGuid();
        var waitlisted = Guid.NewGuid();
        var meritList = MeritList.Generate(
            Guid.NewGuid(),
            [new MeritCandidate(admitted, Guid.NewGuid(), programId, 90), new MeritCandidate(waitlisted, Guid.NewGuid(), programId, 80)],
            new Dictionary<Guid, int> { [programId] = 1 },
            Now).Value;

        var result = meritList.PromoteWaitlisted(waitlisted, programId);

        Assert.True(result.IsSuccess);
        Assert.Equal(MeritOutcome.Admitted, meritList.Entries.Single(e => e.ApplicantId == waitlisted).Outcome);
    }
}
