namespace UMS.Modules.Academic.Domain.Enrollments;

/// <summary>ACD-13: an audited before/after correction entry (ums-requirements.md §4.1's mandatory before/after value requirement).</summary>
public sealed class GradeCorrectionEntry
{
    internal GradeCorrectionEntry(GradeId gradeId, decimal previousScore, decimal newScore, string reason, Guid correctedByUserId, DateTimeOffset correctedAt)
    {
        Id = Guid.NewGuid();
        GradeId = gradeId;
        PreviousScore = previousScore;
        NewScore = newScore;
        Reason = reason;
        CorrectedByUserId = correctedByUserId;
        CorrectedAt = correctedAt;
    }

    private GradeCorrectionEntry()
    {
    }

    public Guid Id { get; private set; }

    public GradeId GradeId { get; private set; }

    public decimal PreviousScore { get; private set; }

    public decimal NewScore { get; private set; }

    public string Reason { get; private set; } = string.Empty;

    public Guid CorrectedByUserId { get; private set; }

    public DateTimeOffset CorrectedAt { get; private set; }
}
