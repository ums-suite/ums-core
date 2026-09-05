using UMS.Modules.Academic.Domain.ResultPublications;

namespace UMS.Modules.Academic.UnitTests.ResultPublications;

/// <summary>
/// requirement-spec.md §4: "Forward-only state machine ... Draft -> Calculated -> Verified ->
/// Approved -> Published -> Archived; the sole exception is the explicit correction cycle, which
/// re-enters at Verified, never skips ahead to Published directly." These are the in-memory
/// legal-transition guards (design-decisions.md "Grade-Lock State Machine Design") - the actual
/// concurrency-safe persistence is a separate, integration-tested concern
/// (<c>ResultPublicationRepository.TryTransitionAsync</c>).
/// </summary>
public sealed class ResultPublicationTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static ResultPublication CreateDraft() => ResultPublication.Create(Guid.NewGuid(), Now);

    [Fact]
    public void Create_starts_in_Draft()
    {
        var resultPublication = CreateDraft();

        Assert.Equal(ResultPublicationStatus.Draft, resultPublication.Status);
    }

    [Fact]
    public void The_full_happy_path_advances_through_every_state_in_order()
    {
        var resultPublication = CreateDraft();

        resultPublication.Submit(Now);
        Assert.Equal(ResultPublicationStatus.Calculated, resultPublication.Status);

        resultPublication.Lock(Guid.NewGuid(), Now);
        Assert.Equal(ResultPublicationStatus.Verified, resultPublication.Status);

        resultPublication.Approve(Guid.NewGuid(), Now);
        Assert.Equal(ResultPublicationStatus.Approved, resultPublication.Status);

        resultPublication.Publish(Guid.NewGuid(), Now);
        Assert.Equal(ResultPublicationStatus.Published, resultPublication.Status);

        resultPublication.Archive(Now);
        Assert.Equal(ResultPublicationStatus.Archived, resultPublication.Status);
    }

    [Fact]
    public void Submit_is_legal_repeatedly_while_still_Calculated_resubmission_before_lock()
    {
        var resultPublication = CreateDraft();
        resultPublication.Submit(Now);

        resultPublication.Submit(Now);

        Assert.Equal(ResultPublicationStatus.Calculated, resultPublication.Status);
    }

    [Theory]
    [InlineData(ResultPublicationStatus.Draft)]
    [InlineData(ResultPublicationStatus.Verified)]
    [InlineData(ResultPublicationStatus.Approved)]
    [InlineData(ResultPublicationStatus.Published)]
    [InlineData(ResultPublicationStatus.Archived)]
    public void Lock_is_illegal_from_every_state_except_Calculated(ResultPublicationStatus notCalculated)
    {
        var resultPublication = CreateDraft();
        AdvanceTo(resultPublication, notCalculated);

        Assert.Throws<InvalidOperationException>(() => resultPublication.Lock(Guid.NewGuid(), Now));
    }

    [Fact]
    public void Publish_cannot_skip_the_Approved_state_directly_from_Verified()
    {
        var resultPublication = CreateDraft();
        resultPublication.Submit(Now);
        resultPublication.Lock(Guid.NewGuid(), Now);

        Assert.Throws<InvalidOperationException>(() => resultPublication.Publish(Guid.NewGuid(), Now));
    }

    [Fact]
    public void Archive_requires_Published_never_a_direct_Approved_to_Archived_skip()
    {
        var resultPublication = CreateDraft();
        resultPublication.Submit(Now);
        resultPublication.Lock(Guid.NewGuid(), Now);
        resultPublication.Approve(Guid.NewGuid(), Now);

        Assert.Throws<InvalidOperationException>(() => resultPublication.Archive(Now));
    }

    [Fact]
    public void ReenterForCorrection_is_legal_only_from_Published_and_moves_to_Verified()
    {
        var resultPublication = CreateDraft();
        resultPublication.Submit(Now);
        resultPublication.Lock(Guid.NewGuid(), Now);
        resultPublication.Approve(Guid.NewGuid(), Now);
        resultPublication.Publish(Guid.NewGuid(), Now);

        resultPublication.ReenterForCorrection(Now);

        Assert.Equal(ResultPublicationStatus.Verified, resultPublication.Status);
        Assert.Equal(1, resultPublication.CorrectionCount);
    }

    [Theory]
    [InlineData(ResultPublicationStatus.Draft)]
    [InlineData(ResultPublicationStatus.Calculated)]
    [InlineData(ResultPublicationStatus.Verified)]
    [InlineData(ResultPublicationStatus.Approved)]
    public void ReenterForCorrection_never_applies_before_Published_the_forward_only_invariants_sole_exception_still_requires_Published_first(ResultPublicationStatus priorState)
    {
        var resultPublication = CreateDraft();
        AdvanceTo(resultPublication, priorState);

        Assert.Throws<InvalidOperationException>(() => resultPublication.ReenterForCorrection(Now));
    }

    [Fact]
    public void Reject_is_legal_only_from_Calculated_and_does_not_change_the_Status()
    {
        var resultPublication = CreateDraft();
        resultPublication.Submit(Now);

        resultPublication.Reject("Scores look wrong", Guid.NewGuid(), Now);

        Assert.Equal(ResultPublicationStatus.Calculated, resultPublication.Status);
        Assert.Equal("Scores look wrong", resultPublication.RejectionReason);
    }

    [Fact]
    public void Reject_from_Draft_throws_since_nothing_has_been_submitted_yet()
    {
        var resultPublication = CreateDraft();

        Assert.Throws<InvalidOperationException>(() => resultPublication.Reject("reason", Guid.NewGuid(), Now));
    }

    [Fact]
    public void Reject_requires_a_non_empty_reason()
    {
        var resultPublication = CreateDraft();
        resultPublication.Submit(Now);

        Assert.Throws<ArgumentException>(() => resultPublication.Reject(" ", Guid.NewGuid(), Now));
    }

    private static void AdvanceTo(ResultPublication resultPublication, ResultPublicationStatus target)
    {
        if (target == ResultPublicationStatus.Draft)
        {
            return;
        }

        resultPublication.Submit(Now);
        if (target == ResultPublicationStatus.Calculated)
        {
            return;
        }

        resultPublication.Lock(Guid.NewGuid(), Now);
        if (target == ResultPublicationStatus.Verified)
        {
            return;
        }

        resultPublication.Approve(Guid.NewGuid(), Now);
        if (target == ResultPublicationStatus.Approved)
        {
            return;
        }

        resultPublication.Publish(Guid.NewGuid(), Now);
        if (target == ResultPublicationStatus.Published)
        {
            return;
        }

        resultPublication.Archive(Now);
    }
}
