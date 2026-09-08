using UMS.Modules.Research.Domain.Events;
using UMS.Modules.Research.Domain.InstitutionalRepositoryEntries;

namespace UMS.Modules.Research.UnitTests.InstitutionalRepositoryEntries;

/// <summary>RES-10/RES-12: requirement-spec.md §2 Institutional Repository, §4/§8 embargo invariants.</summary>
public sealed class InstitutionalRepositoryEntryTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Deposit_accepts_a_Contributor_depositor_with_no_FacultyMemberId_a_graduate_student_case()
    {
        var depositor = new Contributor("Graduate Student", null);

        var entry = InstitutionalRepositoryEntry.Deposit("Thesis Title", RepositoryWorkType.Thesis, depositor, Guid.NewGuid(), Today(), NonEmbargoed(), Now);

        Assert.Null(entry.Depositor.FacultyMemberId);
        Assert.Equal("Graduate Student", entry.Depositor.Name);
    }

    [Fact]
    public void EmbargoPolicy_requires_an_EmbargoEndDate_when_embargoed()
    {
        var result = EmbargoPolicy.Create(true, null, RepositoryAccessLevel.Restricted);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void EmbargoPolicy_forbids_an_EmbargoEndDate_when_not_embargoed()
    {
        var result = EmbargoPolicy.Create(false, Today(), RepositoryAccessLevel.Public);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void LiftEmbargo_clears_IsEmbargoed_and_raises_the_domain_event()
    {
        var entry = InstitutionalRepositoryEntry.Deposit("Title", RepositoryWorkType.Dissertation, new Contributor("Author", null), null, Today(), Embargoed(), Now);

        entry.LiftEmbargo(Now);

        Assert.False(entry.Embargo.IsEmbargoed);
        Assert.Null(entry.Embargo.EmbargoEndDate);
        Assert.Contains(entry.DomainEvents, e => e is InstitutionalRepositoryEntryEmbargoLifted);
    }

    [Fact]
    public void LiftEmbargo_is_a_harmless_no_op_when_already_lifted()
    {
        var entry = InstitutionalRepositoryEntry.Deposit("Title", RepositoryWorkType.Dissertation, new Contributor("Author", null), null, Today(), Embargoed(), Now);
        entry.LiftEmbargo(Now);
        var eventCountAfterFirstLift = entry.DomainEvents.Count;

        entry.LiftEmbargo(Now);

        Assert.Equal(eventCountAfterFirstLift, entry.DomainEvents.Count);
    }

    private static DateOnly Today() => DateOnly.FromDateTime(Now.UtcDateTime);

    private static EmbargoPolicy NonEmbargoed() => EmbargoPolicy.Create(false, null, RepositoryAccessLevel.Public).Value;

    private static EmbargoPolicy Embargoed() => EmbargoPolicy.Create(true, Today().AddDays(30), RepositoryAccessLevel.Restricted).Value;
}
