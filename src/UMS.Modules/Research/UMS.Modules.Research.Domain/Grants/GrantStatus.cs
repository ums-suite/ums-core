namespace UMS.Modules.Research.Domain.Grants;

/// <summary>
/// requirement-spec.md §2 Grant Lifecycle: <c>Proposed -&gt; Funded -&gt; Active -&gt; Closed -&gt;
/// Reported</c>, with <c>Rejected</c> reachable only from <c>Proposed</c> and <c>Withdrawn</c>
/// reachable from <c>Proposed</c>/<c>Funded</c>/<c>Active</c> - both terminal, forward-only, no
/// state skipped (§4).
/// </summary>
public enum GrantStatus
{
    Proposed,
    Funded,
    Active,
    Closed,
    Reported,
    Rejected,
    Withdrawn,
}
