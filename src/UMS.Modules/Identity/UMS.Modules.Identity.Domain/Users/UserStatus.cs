namespace UMS.Modules.Identity.Domain.Users;

/// <summary>A User's authentication eligibility (requirement-spec.md identity §6, `PATCH /users/{id}/status`).</summary>
public enum UserStatus
{
    Active,
    Suspended,
}
