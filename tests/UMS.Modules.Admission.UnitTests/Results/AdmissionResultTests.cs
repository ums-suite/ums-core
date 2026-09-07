using UMS.Modules.Admission.Domain.MeritLists;
using UMS.Modules.Admission.Domain.Results;

namespace UMS.Modules.Admission.UnitTests.Results;

/// <summary>ADM-17: requirement-spec.md §2 Result Publication, §4's result-immutability-post-publish invariant, design-decisions.md's Publishing intermediate state.</summary>
public sealed class AdmissionResultTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static AdmissionResult CalculatedResult()
    {
        var programId = Guid.NewGuid();
        var candidate = new MeritCandidate(Guid.NewGuid(), Guid.NewGuid(), programId, 90);
        var meritList = MeritList.Generate(Guid.NewGuid(), [candidate], new Dictionary<Guid, int> { [programId] = 1 }, Now).Value;
        var result = AdmissionResult.Create(meritList.CampaignId, meritList.Id.Value, Now);
        result.Calculate(meritList.Entries, Now);
        return result;
    }

    [Fact]
    public void Locking_before_calculation_is_rejected()
    {
        var meritList = MeritList.Generate(Guid.NewGuid(), [], new Dictionary<Guid, int>(), Now).Value;
        var result = AdmissionResult.Create(meritList.CampaignId, meritList.Id.Value, Now);

        Assert.Throws<InvalidOperationException>(() => result.Lock(Guid.NewGuid(), Now));
    }

    [Fact]
    public void The_full_lifecycle_reaches_Published_only_via_Publishing()
    {
        var result = CalculatedResult();

        result.Lock(Guid.NewGuid(), Now);
        result.Approve(Guid.NewGuid(), Now);
        result.StartPublishing(Now);
        Assert.Equal(AdmissionResultStatus.Publishing, result.Status);

        var markPublished = result.MarkPublished(Guid.NewGuid(), Now);
        Assert.True(markPublished.IsSuccess);
        Assert.Equal(AdmissionResultStatus.Published, result.Status);
        Assert.Single(result.DomainEvents);
    }

    [Fact]
    public void MarkPublished_is_rejected_when_skipping_the_Publishing_state()
    {
        var result = CalculatedResult();
        result.Lock(Guid.NewGuid(), Now);
        result.Approve(Guid.NewGuid(), Now);

        Assert.Throws<InvalidOperationException>(() => result.MarkPublished(Guid.NewGuid(), Now));
    }

    [Fact]
    public void A_correction_reenters_at_Verified_never_Draft_or_Calculated()
    {
        var result = CalculatedResult();
        result.Lock(Guid.NewGuid(), Now);
        result.Approve(Guid.NewGuid(), Now);
        result.StartPublishing(Now);
        result.MarkPublished(Guid.NewGuid(), Now);

        var reentered = result.ReenterForCorrection(Now);

        Assert.True(reentered.IsSuccess);
        Assert.Equal(AdmissionResultStatus.Verified, result.Status);
        Assert.Equal(1, result.CorrectionCount);
        Assert.Null(result.PublishedAt);
    }
}
