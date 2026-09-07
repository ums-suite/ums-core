namespace UMS.Modules.Academic.Domain.Enrollments;

/// <summary>One raw Assessment score feeding a Grade's calculated aggregate (requirement-spec.md §2 Grade Entry).</summary>
public sealed class AssessmentScoreEntry
{
    internal AssessmentScoreEntry(GradeId gradeId, Guid assessmentId, decimal score)
    {
        Id = Guid.NewGuid();
        GradeId = gradeId;
        AssessmentId = assessmentId;
        Score = score;
    }

    private AssessmentScoreEntry()
    {
    }

    public Guid Id { get; private set; }

    public GradeId GradeId { get; private set; }

    public Guid AssessmentId { get; private set; }

    public decimal Score { get; private set; }
}
