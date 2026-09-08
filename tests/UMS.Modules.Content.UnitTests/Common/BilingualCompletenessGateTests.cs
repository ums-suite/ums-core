using UMS.Modules.Content.Domain.Common;

namespace UMS.Modules.Content.UnitTests.Common;

/// <summary>design-decisions.md "Bilingual-Completeness Gate Enforcement Point" - the shared function itself, independent of which of the three call sites invokes it.</summary>
public sealed class BilingualCompletenessGateTests
{
    [Fact]
    public void Non_public_audience_is_always_exempt()
    {
        Assert.True(BilingualCompletenessGate.IsSatisfied(ContentAudience.Admin, "", "", []));
        Assert.True(BilingualCompletenessGate.IsSatisfied(ContentAudience.Student, "Title", "Body", []));
    }

    [Fact]
    public void Public_audience_requires_non_blank_English_and_a_bn_translation()
    {
        Assert.False(BilingualCompletenessGate.IsSatisfied(ContentAudience.Public, "Title", "Body", []));
        Assert.False(BilingualCompletenessGate.IsSatisfied(ContentAudience.Public, "", "", ["bn"]));
        Assert.True(BilingualCompletenessGate.IsSatisfied(ContentAudience.Public, "Title", "Body", ["bn"]));
    }

    [Fact]
    public void Public_flag_combined_with_other_audiences_still_requires_the_gate()
    {
        var audience = ContentAudience.Public | ContentAudience.Student;

        Assert.False(BilingualCompletenessGate.IsSatisfied(audience, "Title", "Body", []));
        Assert.True(BilingualCompletenessGate.IsSatisfied(audience, "Title", "Body", ["bn"]));
    }

    [Fact]
    public void Language_code_match_is_case_insensitive()
    {
        Assert.True(BilingualCompletenessGate.IsSatisfied(ContentAudience.Public, "Title", "Body", ["BN"]));
    }
}
