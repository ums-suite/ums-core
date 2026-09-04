using UMS.Modules.Organization.Domain.Common;

namespace UMS.Modules.Organization.Domain.Events;

/// <summary>
/// requirement-spec.md organization §3: "Any node's canonical name changes" - one shared event
/// shape across every renamable level (`University`/`Campus`/`Faculty`/`Department`/`Program`;
/// `Designation`/`Building`/`Room` have no `PATCH` at all per §6, so never raise this), dispatched
/// to Audit (synchronous). edge-cases.md "Renaming a node while another module reads a stale
/// cached copy mid-flight" names this event as precisely the mechanism a denormalizing consumer
/// (Reporting, Documents) uses to decide whether to refresh its own cached copy - the event alone
/// is the contract; Organization has no visibility into who, if anyone, is listening.
/// </summary>
public sealed record OrganizationNodeRenamed(
    OrganizationNodeType NodeType,
    Guid NodeId,
    string PreviousName,
    string NewName,
    DateTimeOffset OccurredAt) : IDomainEvent;
