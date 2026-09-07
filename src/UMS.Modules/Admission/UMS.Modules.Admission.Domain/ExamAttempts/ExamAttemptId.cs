namespace UMS.Modules.Admission.Domain.ExamAttempts;

public enum ExamAttemptStatus
{
    InProgress,
    Submitted,
}

public enum EvaluationStatus
{
    Pending,
    Evaluated,
}

/// <summary>ADR-0018: an Admission Officer's outcome on a reviewed <see cref="IntegrityFlag"/> - "a flagged attempt is reviewable evidence ... never an automatic disqualification."</summary>
public enum IntegrityFlagOutcome
{
    Pending,
    Cleared,
    Confirmed,
}

public readonly record struct ExamAttemptId(Guid Value)
{
    public static ExamAttemptId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
