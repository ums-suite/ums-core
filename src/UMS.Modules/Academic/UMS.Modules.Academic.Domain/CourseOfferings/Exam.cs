namespace UMS.Modules.Academic.Domain.CourseOfferings;

/// <summary>ACD-5: an assessment event tied to a CourseOffering (requirement-spec.md §2 Assessment Configuration, §3).</summary>
public sealed class Exam
{
    private readonly List<Assessment> _assessments = [];

    internal Exam(ExamId id, CourseOfferingId courseOfferingId, string name)
    {
        Id = id;
        CourseOfferingId = courseOfferingId;
        Name = name;
    }

    private Exam()
    {
    }

    public ExamId Id { get; private set; }

    public CourseOfferingId CourseOfferingId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public IReadOnlyCollection<Assessment> Assessments => _assessments.AsReadOnly();

    internal Assessment AddAssessment(string name, decimal weight)
    {
        var assessment = new Assessment(AssessmentId.New(), Id, name, weight);
        _assessments.Add(assessment);
        return assessment;
    }
}
