using UMS.Modules.Academic.Domain.Enrollments;
using UMS.Shared.Domain;

namespace UMS.Modules.Academic.UnitTests.Enrollments;

public sealed class GradeTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static Enrollment CreateEnrollmentWithGrade(string letterGrade)
    {
        var enrollment = Enrollment.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), UMS.Modules.Academic.Domain.Common.CreditHours.Create(3).Value, false, null, Now);
        enrollment.SubmitGrade([(Guid.NewGuid(), 70m)], PercentageOrGpa.CreatePercentage(70m).Value, letterGrade, Guid.NewGuid(), Now);
        return enrollment;
    }

    [Theory]
    [InlineData("A+", true)]
    [InlineData("D", true)]
    [InlineData("F", false)]
    [InlineData("f", false)]
    public void IsPassing_is_true_for_any_letter_grade_other_than_F(string letterGrade, bool expectedPassing)
    {
        var enrollment = CreateEnrollmentWithGrade(letterGrade);

        Assert.Equal(expectedPassing, enrollment.Grade!.IsPassing);
    }

    [Fact]
    public void IsPassing_is_false_for_a_never_submitted_Grade()
    {
        var enrollment = Enrollment.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), UMS.Modules.Academic.Domain.Common.CreditHours.Create(3).Value, false, null, Now);
        var grade = enrollment.EnsureGrade();

        Assert.False(grade.IsPassing);
    }

    [Fact]
    public void Correct_requires_a_non_empty_reason()
    {
        var enrollment = CreateEnrollmentWithGrade("B");

        Assert.Throws<ArgumentException>(() => enrollment.Grade!.Correct([(Guid.NewGuid(), 80m)], PercentageOrGpa.CreatePercentage(80m).Value, "A", "  ", Guid.NewGuid(), Now));
    }

    [Fact]
    public void Correct_records_a_correction_history_entry_with_before_after_values()
    {
        var enrollment = CreateEnrollmentWithGrade("B");
        var grade = enrollment.Grade!;

        var previousScore = grade.Correct([(Guid.NewGuid(), 90m)], PercentageOrGpa.CreatePercentage(90m).Value, "A", "Recomputation error", Guid.NewGuid(), Now);

        Assert.Equal(70m, previousScore);
        Assert.Equal("A", grade.LetterGrade);
        Assert.Equal(90m, grade.CalculatedScore!.Value);
        var correction = Assert.Single(grade.Corrections);
        Assert.Equal(70m, correction.PreviousScore);
        Assert.Equal(90m, correction.NewScore);
        Assert.Equal("Recomputation error", correction.Reason);
    }

    [Fact]
    public void Correct_replaces_the_prior_per_assessment_scores_rather_than_leaving_them_stale()
    {
        // Regression test for a genuine bug caught during this flow's manual end-to-end
        // verification: Correct used to update only the derived CalculatedScore/LetterGrade,
        // leaving the per-assessment AssessmentScoreEntry rows - the very data a correction like
        // "transcription error in original marking" is meant to fix - permanently stale.
        var enrollment = CreateEnrollmentWithGrade("B");
        var grade = enrollment.Grade!;
        var correctedAssessmentId = Guid.NewGuid();

        grade.Correct([(correctedAssessmentId, 90m)], PercentageOrGpa.CreatePercentage(90m).Value, "A", "Recomputation error", Guid.NewGuid(), Now);

        var score = Assert.Single(grade.Scores);
        Assert.Equal(correctedAssessmentId, score.AssessmentId);
        Assert.Equal(90m, score.Score);
    }

    [Fact]
    public void Submit_resubmission_replaces_prior_scores_rather_than_accumulating_them()
    {
        var enrollment = Enrollment.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), UMS.Modules.Academic.Domain.Common.CreditHours.Create(3).Value, false, null, Now);
        var assessmentId = Guid.NewGuid();
        enrollment.SubmitGrade([(assessmentId, 50m)], PercentageOrGpa.CreatePercentage(50m).Value, "C", Guid.NewGuid(), Now);

        enrollment.SubmitGrade([(assessmentId, 95m)], PercentageOrGpa.CreatePercentage(95m).Value, "A+", Guid.NewGuid(), Now);

        Assert.Single(enrollment.Grade!.Scores);
        Assert.Equal(95m, enrollment.Grade.Scores.Single().Score);
    }
}
