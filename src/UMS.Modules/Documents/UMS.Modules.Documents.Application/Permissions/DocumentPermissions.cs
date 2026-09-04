namespace UMS.Modules.Documents.Application.Permissions;

/// <summary>
/// Documents' own catalog entries (requirement-spec.md documents §2 Permission Strings, ADR-0006)
/// - shared between the Infrastructure-side manifest and Api-side endpoint gating so the two can
/// never drift (mirrors Audit's own <c>AuditPermissions</c> pattern). <c>document.verify</c> is
/// deliberately absent - it is the one endpoint that requires no permission at all (public).
///
/// <para>
/// <b>Deviation from the requirement-spec's literal strings, documented here:</b> §2 names
/// <c>document.generate</c>, <c>document.read</c>, and <c>document.revoke</c> verbatim - each only
/// two dot-separated segments. Identity's already-built, already-tested Permission catalog
/// (identity requirement-spec.md §2/§6, <c>PermissionCatalogEntry.Register</c>) validates every
/// key against a real <c>&lt;module&gt;.&lt;resource&gt;.&lt;action&gt;</c> pattern requiring at
/// least three segments - a two-segment key is rejected outright at catalog-sync time, confirmed
/// by this module's own integration-test run against the real, running Identity catalog. Rather
/// than weaken Identity's already-shipped validation rule for one module, the three affected keys
/// gain an explicit <c>document</c> resource segment below; <c>document.generate.bulk</c> and
/// <c>document.template.manage</c> already satisfied the pattern as literally specified and are
/// unchanged.
/// </para>
/// </summary>
public static class DocumentPermissions
{
    public const string Generate = "document.document.generate";
    public const string GenerateBulk = "document.generate.bulk";
    public const string Read = "document.document.read";
    public const string TemplateManage = "document.template.manage";
    public const string Revoke = "document.document.revoke";
}
