namespace UMS.Modules.Learning.Domain.Assignments;

public readonly record struct SubmissionExtensionId(Guid Value)
{
    public static SubmissionExtensionId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
