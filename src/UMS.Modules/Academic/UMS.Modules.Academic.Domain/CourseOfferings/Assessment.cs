namespace UMS.Modules.Academic.Domain.CourseOfferings;

/// <summary>ACD-5: a gradable component of an Exam (midterm, final, quiz), carrying a configured weight (requirement-spec.md §2 Assessment Configuration).</summary>
public sealed class Assessment
{
    internal Assessment(AssessmentId id, ExamId examId, string name, decimal weight)
    {
        Id = id;
        ExamId = examId;
        Name = name;
        Weight = weight;
    }

    private Assessment()
    {
    }

    public AssessmentId Id { get; private set; }

    public ExamId ExamId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    /// <summary>A fraction of 1.0 (e.g. 0.30 for 30%) - the sum across every Assessment under one CourseOffering must not exceed 1.0 (enforced by <see cref="CourseOffering.AddAssessment"/>).</summary>
    public decimal Weight { get; private set; }
}
