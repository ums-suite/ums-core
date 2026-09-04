namespace UMS.Modules.Audit.Application.Permissions;

/// <summary>Audit's own catalog entries (requirement-spec.md audit §2/§6) - shared between the Infrastructure-side manifest and Api-side endpoint gating so the two can never drift (mirrors Identity's own <c>IdentityPermissions</c> pattern).</summary>
public static class AuditPermissions
{
    public const string EntryRead = "audit.entry.read";
    public const string ExportGenerate = "audit.export.generate";
}
