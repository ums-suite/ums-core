namespace UMS.Modules.Identity.Domain.Users;

/// <summary>
/// Identity's own strongly-typed id for <see cref="User"/> (ums-conventions.md, Domain Modeling:
/// "every module's own strongly-typed ID ... instead of a bare Guid, so a StudentId can never be
/// passed where a FacultyId is expected"). Every other module that needs to reference "the User
/// behind this Faculty/Alumnus/Student record" stores this same underlying <see cref="Value"/> as
/// an opaque foreign reference, never a joined copy of Identity's own data (ADR-0002).
/// </summary>
public readonly record struct UserId(Guid Value)
{
    public static UserId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
