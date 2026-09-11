using UMS.Modules.Career.Domain.Internships;

namespace UMS.Modules.Career.UnitTests.Internships;

/// <summary>
/// design-decisions.md "Self-Declared CGPA/Year-of-Study Eligibility": `ProgramIds` is the only
/// dimension checked against a real, externally-resolved value (the applying Student's Program);
/// `MinCgpa`/`MinYearOfStudy` are compared only against whatever the Student self-declares - there is
/// no live verification path into Academic anywhere in this type.
/// </summary>
public sealed class EligibilityCriteriaTests
{
    [Fact]
    public void None_places_no_restriction_on_any_dimension()
    {
        Assert.True(EligibilityCriteria.None.IsSatisfiedByProgram(Guid.NewGuid()));
        Assert.True(EligibilityCriteria.None.IsSatisfiedByDeclaredValues(null, null));
    }

    [Fact]
    public void IsSatisfiedByProgram_restricts_to_the_named_programs_only()
    {
        var programId = Guid.NewGuid();
        var criteria = new EligibilityCriteria([programId], null, null);

        Assert.True(criteria.IsSatisfiedByProgram(programId));
        Assert.False(criteria.IsSatisfiedByProgram(Guid.NewGuid()));
    }

    [Theory]
    [InlineData(3.0, 3.5, false)]
    [InlineData(3.5, 3.5, true)]
    [InlineData(4.0, 3.5, true)]
    [InlineData(null, 3.5, false)]
    public void IsSatisfiedByDeclaredValues_enforces_a_minimum_CGPA_against_the_self_declared_value(double? declared, double min, bool expected)
    {
        var criteria = new EligibilityCriteria([], (decimal)min, null);
        Assert.Equal(expected, criteria.IsSatisfiedByDeclaredValues(declared is null ? null : (decimal)declared.Value, null));
    }

    [Theory]
    [InlineData(2, 3, false)]
    [InlineData(3, 3, true)]
    [InlineData(4, 3, true)]
    [InlineData(null, 3, false)]
    public void IsSatisfiedByDeclaredValues_enforces_a_minimum_year_of_study_against_the_self_declared_value(int? declared, int min, bool expected)
    {
        var criteria = new EligibilityCriteria([], null, min);
        Assert.Equal(expected, criteria.IsSatisfiedByDeclaredValues(null, declared));
    }
}
