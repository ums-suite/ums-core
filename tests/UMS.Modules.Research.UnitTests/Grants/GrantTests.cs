using UMS.Modules.Research.Domain.Common;
using UMS.Modules.Research.Domain.Events;
using UMS.Modules.Research.Domain.Grants;
using UMS.Shared.Domain;

namespace UMS.Modules.Research.UnitTests.Grants;

/// <summary>RES-2/RES-3/RES-4/RES-5: requirement-spec.md §2 Grant Lifecycle, §4 domain invariants.</summary>
public sealed class GrantTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Propose_creates_a_Proposed_Grant_with_the_named_FacultyMember_as_sole_Principal_Investigator()
    {
        var pi = Guid.NewGuid();
        var grant = CreateProposed(pi);

        Assert.Equal(GrantStatus.Proposed, grant.Status);
        Assert.Equal(pi, grant.PrincipalInvestigatorFacultyMemberId);
        Assert.Single(grant.Investigators);
        Assert.False(grant.RequiresPiReassignment);
    }

    [Fact]
    public void Fund_then_Activate_then_Close_then_Report_walks_the_full_forward_lifecycle()
    {
        var grant = CreateProposed(Guid.NewGuid());

        grant.Fund(DateOnly.FromDateTime(Now.UtcDateTime), Money.Create(1000m, "USD").Value, Period(), Now);
        Assert.Equal(GrantStatus.Funded, grant.Status);

        grant.Activate(Now);
        Assert.Equal(GrantStatus.Active, grant.Status);

        grant.Close(Now);
        Assert.Equal(GrantStatus.Closed, grant.Status);

        grant.Report(Now);
        Assert.Equal(GrantStatus.Reported, grant.Status);
    }

    [Fact]
    public void Reject_is_only_reachable_from_Proposed()
    {
        var grant = CreateProposed(Guid.NewGuid());
        grant.Fund(DateOnly.FromDateTime(Now.UtcDateTime), Money.Create(1000m).Value, Period(), Now);

        Assert.Throws<InvalidOperationException>(() => grant.Reject(Now));
    }

    [Theory]
    [InlineData(GrantStatus.Proposed)]
    [InlineData(GrantStatus.Funded)]
    [InlineData(GrantStatus.Active)]
    public void Withdraw_is_reachable_from_Proposed_Funded_or_Active(GrantStatus status)
    {
        var grant = CreateProposed(Guid.NewGuid());
        Advance(grant, status);

        grant.Withdraw(Now);

        Assert.Equal(GrantStatus.Withdrawn, grant.Status);
    }

    [Fact]
    public void FlagPiReassignmentRequired_blocks_forward_transitions_other_than_the_two_resolution_paths()
    {
        // design-decisions.md's PI-vacancy decision: every forward lifecycle transition is blocked
        // EXCEPT Close/Withdraw (the two ways to resolve the vacancy without reassigning) - Activate
        // is re-asserted against a second, still-Funded Grant since the first is consumed by Close
        // below.
        var pi = Guid.NewGuid();
        var grant = CreateProposed(pi);
        grant.Fund(DateOnly.FromDateTime(Now.UtcDateTime), Money.Create(1000m).Value, Period(), Now);
        grant.FlagPiReassignmentRequired(pi, Now);

        Assert.True(grant.RequiresPiReassignment);
        Assert.Throws<InvalidOperationException>(() => grant.Activate(Now));
        Assert.Throws<InvalidOperationException>(() => grant.Report(Now));
    }

    [Fact]
    public void Close_is_permitted_while_PI_vacant_and_clears_the_flag()
    {
        var pi = Guid.NewGuid();
        var grant = CreateProposed(pi);
        grant.Fund(DateOnly.FromDateTime(Now.UtcDateTime), Money.Create(1000m).Value, Period(), Now);
        grant.Activate(Now);
        grant.FlagPiReassignmentRequired(pi, Now);

        grant.Close(Now);

        Assert.Equal(GrantStatus.Closed, grant.Status);
        Assert.False(grant.RequiresPiReassignment);
    }

    [Fact]
    public void Withdraw_is_permitted_while_PI_vacant_and_clears_the_flag()
    {
        var pi = Guid.NewGuid();
        var grant = CreateProposed(pi);
        grant.Fund(DateOnly.FromDateTime(Now.UtcDateTime), Money.Create(1000m).Value, Period(), Now);
        grant.Activate(Now);
        grant.FlagPiReassignmentRequired(pi, Now);

        grant.Withdraw(Now);

        Assert.Equal(GrantStatus.Withdrawn, grant.Status);
        Assert.False(grant.RequiresPiReassignment);
    }

    [Fact]
    public void FlagPiReassignmentRequired_is_idempotent_and_only_raises_the_domain_event_once()
    {
        var pi = Guid.NewGuid();
        var grant = CreateProposed(pi);
        grant.Fund(DateOnly.FromDateTime(Now.UtcDateTime), Money.Create(1000m).Value, Period(), Now);
        grant.Activate(Now);

        grant.FlagPiReassignmentRequired(pi, Now);
        grant.FlagPiReassignmentRequired(pi, Now);

        Assert.True(grant.RequiresPiReassignment);
        Assert.Single(grant.DomainEvents, e => e is GrantPiReassignmentRequired);
    }

    [Fact]
    public void FlagPiReassignmentRequired_does_nothing_for_a_FacultyMember_who_is_not_the_current_PI()
    {
        var pi = Guid.NewGuid();
        var someoneElse = Guid.NewGuid();
        var grant = CreateProposed(pi);
        grant.Fund(DateOnly.FromDateTime(Now.UtcDateTime), Money.Create(1000m).Value, Period(), Now);
        grant.Activate(Now);

        grant.FlagPiReassignmentRequired(someoneElse, Now);

        Assert.False(grant.RequiresPiReassignment);
    }

    [Fact]
    public void AddInvestigator_with_PrincipalInvestigator_role_reassigns_the_PI_and_clears_the_vacancy_flag()
    {
        var originalPi = Guid.NewGuid();
        var newPi = Guid.NewGuid();
        var grant = CreateProposed(originalPi);
        grant.Fund(DateOnly.FromDateTime(Now.UtcDateTime), Money.Create(1000m).Value, Period(), Now);
        grant.Activate(Now);
        grant.FlagPiReassignmentRequired(originalPi, Now);

        grant.AddInvestigator(newPi, GrantInvestigatorRole.PrincipalInvestigator, Now);

        Assert.Equal(newPi, grant.PrincipalInvestigatorFacultyMemberId);
        Assert.False(grant.RequiresPiReassignment);
        Assert.Single(grant.Investigators);
    }

    [Fact]
    public void AddInvestigator_as_CoInvestigator_appends_without_disturbing_the_PI()
    {
        var pi = Guid.NewGuid();
        var coI = Guid.NewGuid();
        var grant = CreateProposed(pi);

        grant.AddInvestigator(coI, GrantInvestigatorRole.CoInvestigator, Now);

        Assert.Equal(2, grant.Investigators.Count);
        Assert.Equal(pi, grant.PrincipalInvestigatorFacultyMemberId);
    }

    [Fact]
    public void AddInvestigator_rejects_a_duplicate_CoInvestigator()
    {
        var pi = Guid.NewGuid();
        var coI = Guid.NewGuid();
        var grant = CreateProposed(pi);
        grant.AddInvestigator(coI, GrantInvestigatorRole.CoInvestigator, Now);

        Assert.Throws<InvalidOperationException>(() => grant.AddInvestigator(coI, GrantInvestigatorRole.CoInvestigator, Now));
    }

    [Fact]
    public void RemoveInvestigator_disallows_removing_the_PI_directly_once_Funded_unless_vacant()
    {
        var pi = Guid.NewGuid();
        var grant = CreateProposed(pi);
        grant.Fund(DateOnly.FromDateTime(Now.UtcDateTime), Money.Create(1000m).Value, Period(), Now);

        Assert.Throws<InvalidOperationException>(() => grant.RemoveInvestigator(pi));
    }

    [Fact]
    public void RemoveInvestigator_permits_removing_the_PI_while_Proposed()
    {
        var pi = Guid.NewGuid();
        var grant = CreateProposed(pi);

        grant.RemoveInvestigator(pi);

        Assert.Empty(grant.Investigators);
    }

    private static Grant CreateProposed(Guid principalInvestigatorFacultyMemberId) =>
        Grant.Propose("Title", "Description", Guid.NewGuid(), Money.Create(1000m).Value, Period(), principalInvestigatorFacultyMemberId, Now);

    private static DateRange Period() => DateRange.Create(DateOnly.FromDateTime(Now.UtcDateTime), DateOnly.FromDateTime(Now.UtcDateTime).AddYears(1)).Value;

    private static void Advance(Grant grant, GrantStatus target)
    {
        if (target is GrantStatus.Funded or GrantStatus.Active)
        {
            grant.Fund(DateOnly.FromDateTime(Now.UtcDateTime), Money.Create(1000m).Value, Period(), Now);
        }

        if (target is GrantStatus.Active)
        {
            grant.Activate(Now);
        }
    }
}
