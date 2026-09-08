namespace UMS.Modules.Library.Application.Permissions;

/// <summary>
/// requirement-spec.md §6 API surface, gated per ADR-0012/Identity's catalog convention
/// (<c>&lt;module&gt;.&lt;resource&gt;.&lt;action&gt;</c>, at least three dot-separated segments,
/// each segment lowercase letters/digits ONLY - no underscore, per the regex
/// <c>^[a-z][a-z0-9]*(\.[a-z][a-z0-9]*){2,}$</c>. This exact mistake (an underscore inside a
/// segment) broke Hostel's whole-solution test suite the moment its own WebApplicationFactory host
/// tried to sync the full permission catalog at startup - every constant below was checked against
/// that regex before being written.
/// </summary>
public static class LibraryPermissions
{
    public const string CatalogManage = "library.catalog.manage";
    public const string LoanIssue = "library.loan.issue";
    public const string LoanManage = "library.loan.manage";
    public const string FineWaive = "library.fine.waive";
    public const string CopyWriteOff = "library.copy.writeoff";
    public const string ReviewFlagView = "library.review.view";
}
