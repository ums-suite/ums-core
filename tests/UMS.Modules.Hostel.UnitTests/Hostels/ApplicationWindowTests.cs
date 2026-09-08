using UMS.Modules.Hostel.Domain.ApplicationWindows;

namespace UMS.Modules.Hostel.UnitTests.Hostels;

/// <summary>HOS-2: requirement-spec.md §2 step 1, step 3; §9 decision 4; §8 edge case "window closed -&gt; rejected with machine-readable error".</summary>
public sealed class ApplicationWindowTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 0, 0, 0, TimeSpan.Zero);

    private static ApplicationWindow Window() =>
        ApplicationWindow.Create("2026-Spring", Now.AddDays(-1), Now.AddDays(30), Now).Value;

    [Fact]
    public void Create_with_closesAt_before_opensAt_is_rejected()
    {
        var result = ApplicationWindow.Create("bad", Now, Now.AddDays(-1), Now);

        Assert.True(result.IsFailure);
        Assert.Equal("application_window.invalid_range", result.Error!.Code);
    }

    [Fact]
    public void IsOpenAt_before_opensAt_is_false()
    {
        var window = Window();

        Assert.False(window.IsOpenAt(Now.AddDays(-2)));
    }

    [Fact]
    public void IsOpenAt_after_closesAt_is_false_edge_case_application_after_window_closes()
    {
        var window = Window();

        Assert.False(window.IsOpenAt(Now.AddDays(31)));
    }

    [Fact]
    public void IsOpenAt_within_range_is_true()
    {
        var window = Window();

        Assert.True(window.IsOpenAt(Now));
    }

    [Fact]
    public void AllowsProgram_with_empty_list_allows_every_program()
    {
        var window = Window();

        Assert.True(window.AllowsProgram(Guid.NewGuid()));
    }

    [Fact]
    public void AllowsProgram_with_a_restricted_list_rejects_an_unlisted_program()
    {
        var window = Window();
        var allowedProgramId = Guid.NewGuid();
        window.ReplaceEligiblePrograms([allowedProgramId]);

        Assert.True(window.AllowsProgram(allowedProgramId));
        Assert.False(window.AllowsProgram(Guid.NewGuid()));
    }

    [Fact]
    public void ReplaceEligibilityRules_increments_RulesVersion_requirement_spec_versioned_rule_set()
    {
        var window = Window();
        var initialVersion = window.RulesVersion;

        window.ReplaceEligibilityRules([EligibilityRuleDefinition.Create(HostelEligibilityRuleType.MinimumYearOfStudy, 2, "Minimum year 2").Value]);

        Assert.Equal(initialVersion + 1, window.RulesVersion);
    }

    [Fact]
    public void ReplaceEligibleYears_rejects_a_non_positive_year()
    {
        var window = Window();

        var result = window.ReplaceEligibleYears([1, 0]);

        Assert.True(result.IsFailure);
        Assert.Equal("application_window.invalid_year", result.Error!.Code);
    }
}
