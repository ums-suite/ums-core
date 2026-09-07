using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Learning.Domain.Submissions;

/// <summary>
/// requirement-spec.md learning §3 (module-local, pending glossary merge): <c>points</c>/
/// <c>maxPoints</c> plus the Instructor's free-text feedback, recorded on a
/// <see cref="Submission"/> via <see cref="Submission.Evaluate"/> - never a raw field write.
///
/// <para>
/// <see cref="AwardedPoints"/> is the score AFTER the late-penalty deduction the
/// <c>SubmissionWindow</c>'s own tier schedule resolved at submission time;
/// <see cref="RawPoints"/> is what the Instructor actually entered. Keeping both means a Student
/// disputing a deduction can see exactly what was marked and exactly what was subtracted, rather
/// than only the net figure.
/// </para>
/// </summary>
public sealed record AssignmentScore
{
    private AssignmentScore(decimal rawPoints, decimal awardedPoints, int maxPoints, decimal appliedLatePenaltyPercentage, string feedback)
    {
        RawPoints = rawPoints;
        AwardedPoints = awardedPoints;
        MaxPoints = maxPoints;
        AppliedLatePenaltyPercentage = appliedLatePenaltyPercentage;
        Feedback = feedback;
    }

    public decimal RawPoints { get; }

    public decimal AwardedPoints { get; }

    public int MaxPoints { get; }

    public decimal AppliedLatePenaltyPercentage { get; }

    public string Feedback { get; }

    internal static Result<AssignmentScore> Create(decimal rawPoints, int maxPoints, decimal appliedLatePenaltyPercentage, string? feedback)
    {
        if (maxPoints < 1)
        {
            return Error.Validation("assignment_score.max_points_out_of_range", "An AssignmentScore's maxPoints must be at least 1.");
        }

        if (rawPoints < 0m || rawPoints > maxPoints)
        {
            return Error.Validation("assignment_score.points_out_of_range", $"An AssignmentScore's points must be between 0 and {maxPoints}.");
        }

        if (appliedLatePenaltyPercentage is < 0m or > 100m)
        {
            return Error.Validation("assignment_score.penalty_out_of_range", "An AssignmentScore's applied late-penalty percentage must be between 0 and 100.");
        }

        var awarded = decimal.Round(rawPoints * (100m - appliedLatePenaltyPercentage) / 100m, 2, MidpointRounding.AwayFromZero);
        return new AssignmentScore(rawPoints, awarded, maxPoints, appliedLatePenaltyPercentage, (feedback ?? string.Empty).Trim());
    }
}
