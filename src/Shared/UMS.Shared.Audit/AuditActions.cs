namespace UMS.Shared.Audit;

/// <summary>
/// The fixed base action vocabulary every module reuses (requirement-spec.md audit §2: "a fixed
/// vocabulary... extensible per module but never free text"). A calling module may pass any other
/// lowercase, snake_case token instead of one of these constants for a module-specific verb (e.g.
/// Finance's own "refund") - <see cref="AuditAction"/>'s own validation is what actually prevents
/// free text, not this list; these constants exist purely so calling modules don't have to
/// hand-type the eight most common verbs.
/// </summary>
public static class AuditActions
{
    public const string Create = "create";
    public const string Update = "update";
    public const string Delete = "delete";
    public const string Publish = "publish";
    public const string Approve = "approve";
    public const string Reject = "reject";
    public const string Assign = "assign";
    public const string Revoke = "revoke";
}
