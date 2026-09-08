namespace UMS.Modules.Research.Application.Permissions;

/// <summary>
/// requirement-spec.md §2 Permission Strings (ADR-0006), verified against Identity's actual catalog
/// validator (<c>^[a-z][a-z0-9]*(\.[a-z][a-z0-9]*){2,}$</c> - at least 3 dot-separated segments, each
/// lowercase letters/digits only, no underscore; see ums-core-gotchas memory). Every string below
/// was checked character-by-character: none contains an underscore or an uppercase/mixed segment,
/// so every one of requirement-spec §2's literal strings (including the 4-segment
/// <c>research.grant.investigators.manage</c>) already satisfies the validator verbatim - no
/// collapsing/restructuring needed.
/// </summary>
public static class ResearchPermissions
{
    public const string FundingBodyManage = "research.fundingbody.manage";

    public const string GrantPropose = "research.grant.propose";

    /// <summary>Gates fund/activate/close/report/reject/withdraw for Research-Office/Admin. <c>activate</c>/<c>close</c> are ALSO available to the owning PI - see <c>GrantEndpoints</c>'s own ownership check, layered on top of this permission for the PI's own case.</summary>
    public const string GrantManage = "research.grant.manage";

    public const string GrantInvestigatorsManage = "research.grant.investigators.manage";

    public const string GrantRead = "research.grant.read";

    public const string PublicationCreate = "research.publication.create";

    public const string PublicationUpdate = "research.publication.update";

    /// <summary>Admin-only, duplicate resolution (RES-9).</summary>
    public const string PublicationMerge = "research.publication.merge";

    public const string PublicationRead = "research.publication.read";

    public const string RepositoryDeposit = "research.repository.deposit";

    /// <summary>Admin-only embargo override (RES-11).</summary>
    public const string RepositoryManage = "research.repository.manage";

    public const string RepositoryRead = "research.repository.read";
}
