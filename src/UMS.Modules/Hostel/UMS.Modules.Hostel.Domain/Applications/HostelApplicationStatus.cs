namespace UMS.Modules.Hostel.Domain.Applications;

/// <summary>
/// requirement-spec.md §3: <c>Draft -&gt; Submitted -&gt; Ranked -&gt; Approved/Waitlisted/Rejected</c>,
/// plus <see cref="Withdrawn"/> - a genuine spec gap edge-cases.md resolves (design-decisions.md
/// "HostelApplication Withdrawal as a First-Class State Transition"), reachable only from
/// <see cref="Draft"/>/<see cref="Submitted"/>/<see cref="Ranked"/>/<see cref="Waitlisted"/>, never
/// from <see cref="Approved"/>.
/// </summary>
public enum HostelApplicationStatus
{
    Draft,
    Submitted,
    Ranked,
    Approved,
    Waitlisted,
    Rejected,
    Withdrawn,
}
