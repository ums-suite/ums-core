using UMS.Modules.Finance.Domain.Common;
using UMS.Modules.Finance.Domain.FeeStructures;

namespace UMS.Modules.Finance.UnitTests.FeeStructures;

/// <summary>FIN-1: requirement-spec.md §2 "a change to an in-effect FeeStructure never retroactively alters an already-generated Invoice" - the versioning mechanism this test class exercises is what makes that possible.</summary>
public sealed class FeeStructureTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void An_empty_fee_type_is_rejected()
    {
        var applicability = FeeApplicability.ForService("Admission").Value;

        var result = FeeStructure.CreateInitialVersion(string.Empty, applicability, Money.Create(500m).Value, Now, Now);

        Assert.True(result.IsFailure);
        Assert.Equal("fee_structure.fee_type_required", result.Error!.Code);
    }

    [Fact]
    public void The_initial_version_is_Active_with_version_number_one()
    {
        var applicability = FeeApplicability.ForService("Admission").Value;

        var structure = FeeStructure.CreateInitialVersion("ApplicationFee", applicability, Money.Create(500m).Value, Now, Now).Value;

        Assert.Equal(1, structure.VersionNumber);
        Assert.Equal(FeeStructureStatus.Active, structure.Status);
        Assert.Contains(structure.DomainEvents, e => e is Domain.Events.FeeStructurePublished);
    }

    [Fact]
    public void IsEffectiveAt_is_false_before_the_effective_from_instant()
    {
        var applicability = FeeApplicability.ForService("Admission").Value;
        var structure = FeeStructure.CreateInitialVersion("ApplicationFee", applicability, Money.Create(500m).Value, Now, Now).Value;

        Assert.False(structure.IsEffectiveAt(Now.AddDays(-1)));
        Assert.True(structure.IsEffectiveAt(Now));
    }

    /// <summary>A Deprecated row is never "effective" again once superseded, regardless of the date queried - the next version is the one an Invoice should resolve against.</summary>
    [Fact]
    public void IsEffectiveAt_is_false_for_a_Deprecated_structure_even_within_its_former_date_range()
    {
        var applicability = FeeApplicability.ForService("Admission").Value;
        var structure = FeeStructure.CreateInitialVersion("ApplicationFee", applicability, Money.Create(500m).Value, Now, Now).Value;

        structure.Deprecate(Now.AddDays(10), Now.AddDays(10));

        Assert.False(structure.IsEffectiveAt(Now.AddDays(5)));
        Assert.False(structure.IsEffectiveAt(Now.AddDays(10)));
    }

    [Fact]
    public void CreateNewVersion_increments_the_version_number_and_keeps_the_same_fee_type_and_applicability()
    {
        var applicability = FeeApplicability.ForService("Admission").Value;
        var v1 = FeeStructure.CreateInitialVersion("ApplicationFee", applicability, Money.Create(500m).Value, Now, Now).Value;

        var v2 = v1.CreateNewVersion(Money.Create(600m).Value, Now.AddDays(30), Now.AddDays(30)).Value;

        Assert.Equal(2, v2.VersionNumber);
        Assert.Equal(v1.FeeType, v2.FeeType);
        Assert.Equal(600m, v2.Amount.Amount);
    }

    [Fact]
    public void CreateNewVersion_against_an_already_Deprecated_structure_is_rejected()
    {
        var applicability = FeeApplicability.ForService("Admission").Value;
        var v1 = FeeStructure.CreateInitialVersion("ApplicationFee", applicability, Money.Create(500m).Value, Now, Now).Value;
        v1.Deprecate(Now.AddDays(1), Now.AddDays(1));

        var result = v1.CreateNewVersion(Money.Create(600m).Value, Now.AddDays(2), Now.AddDays(2));

        Assert.True(result.IsFailure);
        Assert.Equal("fee_structure.not_active", result.Error!.Code);
    }

    [Fact]
    public void Deprecating_an_already_Deprecated_structure_is_rejected()
    {
        var applicability = FeeApplicability.ForService("Admission").Value;
        var structure = FeeStructure.CreateInitialVersion("ApplicationFee", applicability, Money.Create(500m).Value, Now, Now).Value;
        structure.Deprecate(Now.AddDays(1), Now.AddDays(1));

        var result = structure.Deprecate(Now.AddDays(2), Now.AddDays(2));

        Assert.True(result.IsFailure);
        Assert.Equal("fee_structure.not_active", result.Error!.Code);
    }
}
