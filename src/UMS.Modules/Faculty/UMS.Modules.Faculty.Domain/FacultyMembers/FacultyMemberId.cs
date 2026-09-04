namespace UMS.Modules.Faculty.Domain.FacultyMembers;

public readonly record struct FacultyMemberId(Guid Value)
{
    public static FacultyMemberId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
