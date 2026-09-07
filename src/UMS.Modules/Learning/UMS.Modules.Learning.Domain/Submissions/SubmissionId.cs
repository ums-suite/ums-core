namespace UMS.Modules.Learning.Domain.Submissions;

public readonly record struct SubmissionId(Guid Value)
{
    public static SubmissionId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
