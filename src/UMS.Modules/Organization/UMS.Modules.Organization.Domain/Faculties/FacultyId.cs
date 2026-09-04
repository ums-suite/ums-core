namespace UMS.Modules.Organization.Domain.Faculties;

public readonly record struct FacultyId(Guid Value)
{
    public static FacultyId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
