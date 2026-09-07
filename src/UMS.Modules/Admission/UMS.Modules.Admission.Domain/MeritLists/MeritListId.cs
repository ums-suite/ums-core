namespace UMS.Modules.Admission.Domain.MeritLists;

public enum MeritListStatus
{
    Draft,
    Approved,
}

public enum MeritOutcome
{
    Admitted,
    Waitlisted,
    Rejected,
}

public readonly record struct MeritListId(Guid Value)
{
    public static MeritListId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
