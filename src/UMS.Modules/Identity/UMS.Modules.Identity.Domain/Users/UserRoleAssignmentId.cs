namespace UMS.Modules.Identity.Domain.Users;

public readonly record struct UserRoleAssignmentId(Guid Value)
{
    public static UserRoleAssignmentId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
