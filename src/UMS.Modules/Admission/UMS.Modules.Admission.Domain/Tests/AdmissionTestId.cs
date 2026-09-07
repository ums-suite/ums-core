namespace UMS.Modules.Admission.Domain.Tests;

public enum QuestionDifficulty
{
    Easy,
    Medium,
    Hard,
}

public readonly record struct AdmissionTestId(Guid Value)
{
    public static AdmissionTestId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}

public readonly record struct TestSlotId(Guid Value)
{
    public static TestSlotId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}

public readonly record struct QuestionId(Guid Value)
{
    public static QuestionId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
