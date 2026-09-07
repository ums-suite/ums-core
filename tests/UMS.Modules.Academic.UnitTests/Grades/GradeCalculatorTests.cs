using UMS.Modules.Academic.Application.Grades;
using UMS.Modules.Academic.Domain.CourseOfferings;

namespace UMS.Modules.Academic.UnitTests.Grades;

/// <summary>ACD-10: "the system calculates the aggregate Grade per the CourseOffering's configured weighting" (requirement-spec.md §2).</summary>
public sealed class GradeCalculatorTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static CourseOffering CreateOfferingWithAssessments(out Guid midtermAssessmentId, out Guid finalAssessmentId)
    {
        var offering = CourseOffering.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 30, Now);
        var exam = offering.AddExam("Combined", [("Midterm", 0.4m), ("Final", 0.6m)]);
        var assessments = exam.Assessments.ToList();
        midtermAssessmentId = assessments[0].Id.Value;
        finalAssessmentId = assessments[1].Id.Value;
        return offering;
    }

    [Fact]
    public void Calculate_produces_the_weighted_average_of_all_submitted_scores()
    {
        var offering = CreateOfferingWithAssessments(out var midtermId, out var finalId);

        var result = GradeCalculator.Calculate(offering, [(midtermId, 80m), (finalId, 70m)]);

        Assert.True(result.IsSuccess);
        Assert.Equal(74m, result.Value.CalculatedScore.Value); // 80*0.4 + 70*0.6 = 74
    }

    [Theory]
    [InlineData(90, "A+")]
    [InlineData(72, "A-")]
    [InlineData(42, "D")]
    [InlineData(10, "F")]
    public void Calculate_maps_the_score_to_the_correct_letter_band(decimal uniformScore, string expectedLetter)
    {
        var offering = CreateOfferingWithAssessments(out var midtermId, out var finalId);

        var result = GradeCalculator.Calculate(offering, [(midtermId, uniformScore), (finalId, uniformScore)]);

        Assert.Equal(expectedLetter, result.Value.LetterGrade);
    }

    [Fact]
    public void Calculate_rejects_a_score_for_an_Assessment_not_configured_on_the_CourseOffering()
    {
        var offering = CreateOfferingWithAssessments(out _, out _);

        var result = GradeCalculator.Calculate(offering, [(Guid.NewGuid(), 80m)]);

        Assert.True(result.IsFailure);
        Assert.Equal("grade.unknown_assessment", result.Error!.Code);
    }

    [Fact]
    public void Calculate_rejects_a_CourseOffering_with_no_Assessments_configured()
    {
        var offering = CourseOffering.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 30, Now);

        var result = GradeCalculator.Calculate(offering, [(Guid.NewGuid(), 80m)]);

        Assert.True(result.IsFailure);
        Assert.Equal("grade.no_assessments_configured", result.Error!.Code);
    }

    [Fact]
    public void Calculate_rejects_an_out_of_range_score()
    {
        var offering = CreateOfferingWithAssessments(out var midtermId, out _);

        var result = GradeCalculator.Calculate(offering, [(midtermId, 150m)]);

        Assert.True(result.IsFailure);
        Assert.Equal("grade.score_out_of_range", result.Error!.Code);
    }

    [Fact]
    public void Calculate_normalizes_a_partial_submission_against_only_the_weight_actually_submitted()
    {
        var offering = CreateOfferingWithAssessments(out var midtermId, out _);

        var result = GradeCalculator.Calculate(offering, [(midtermId, 80m)]);

        Assert.True(result.IsSuccess);
        Assert.Equal(80m, result.Value.CalculatedScore.Value);
    }
}
