namespace UMS.Modules.Admission.Domain.ExamAttempts;

/// <summary>
/// ADR-0018: "continuous flagging ... surfaced as a reviewable IntegrityFlag, never an automatic
/// fail." Owned by <see cref="ExamAttempt"/> itself (ADR-0018: "the provider integration is
/// shared, the domain data is not").
/// </summary>
public sealed class IntegrityFlag
{
    internal IntegrityFlag(Guid id, string anomalyType, string details, decimal confidenceScore, DateTimeOffset raisedAt)
    {
        Id = id;
        AnomalyType = anomalyType;
        Details = details;
        ConfidenceScore = confidenceScore;
        RaisedAt = raisedAt;
        Outcome = IntegrityFlagOutcome.Pending;
    }

    private IntegrityFlag()
    {
    }

    public Guid Id { get; private set; }

    public string AnomalyType { get; private set; } = string.Empty;

    public string Details { get; private set; } = string.Empty;

    public decimal ConfidenceScore { get; private set; }

    public DateTimeOffset RaisedAt { get; private set; }

    public IntegrityFlagOutcome Outcome { get; private set; }

    public Guid? ReviewedByUserId { get; private set; }

    public string? ReviewNotes { get; private set; }

    public DateTimeOffset? ReviewedAt { get; private set; }

    /// <summary>ADR-0018: "a human always makes the final call on a flagged attempt" - the Admission Officer's own review, never automatic.</summary>
    internal void Review(IntegrityFlagOutcome outcome, Guid reviewedByUserId, string? notes, DateTimeOffset now)
    {
        Outcome = outcome;
        ReviewedByUserId = reviewedByUserId;
        ReviewNotes = notes;
        ReviewedAt = now;
    }
}
