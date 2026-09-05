using UMS.Modules.Academic.Domain.CourseOfferings;
using UMS.Shared.Domain;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Academic.Application.Grades;

/// <summary>ACD-10: "the system calculates the aggregate Grade per the CourseOffering's configured weighting" (requirement-spec.md §2 Grade Entry). A pure, stateless calculation - not itself state, so it lives as an Application-layer policy rather than on an entity.</summary>
public static class GradeCalculator
{
    private static readonly (decimal MinPercentage, string Letter)[] LetterBands =
    [
        (80, "A+"), (75, "A"), (70, "A-"), (65, "B+"), (60, "B"),
        (55, "B-"), (50, "C+"), (45, "C"), (40, "D"), (0, "F"),
    ];

    public static Result<(PercentageOrGpa CalculatedScore, string LetterGrade)> Calculate(
        CourseOffering courseOffering,
        IReadOnlyCollection<(Guid AssessmentId, decimal Score)> scores)
    {
        var assessments = courseOffering.Exams.SelectMany(e => e.Assessments).ToDictionary(a => a.Id.Value);
        if (assessments.Count == 0)
        {
            return Error.Validation("grade.no_assessments_configured", $"CourseOffering '{courseOffering.Id}' has no Assessments configured to grade against.");
        }

        decimal weightedTotal = 0;
        decimal totalWeightSeen = 0;
        foreach (var (assessmentId, score) in scores)
        {
            if (!assessments.TryGetValue(assessmentId, out var assessment))
            {
                return Error.Validation("grade.unknown_assessment", $"Assessment '{assessmentId}' is not configured for CourseOffering '{courseOffering.Id}'.");
            }

            if (score < 0 || score > 100)
            {
                return Error.Validation("grade.score_out_of_range", $"Assessment score for '{assessmentId}' must be between 0 and 100.");
            }

            weightedTotal += score * assessment.Weight;
            totalWeightSeen += assessment.Weight;
        }

        // Normalizes against the weight actually submitted, so a partial submission (not every
        // configured Assessment scored yet) still produces a meaningful percentage rather than
        // silently under-counting against the CourseOffering's full configured weight.
        var percentage = totalWeightSeen == 0 ? 0 : Math.Round(weightedTotal / totalWeightSeen, 2);

        var scoreResult = PercentageOrGpa.CreatePercentage(percentage);
        if (scoreResult.IsFailure)
        {
            return scoreResult.Error!;
        }

        var letter = LetterBands.First(band => percentage >= band.MinPercentage).Letter;
        return (scoreResult.Value, letter);
    }
}
