namespace UMS.Shared.Research;

/// <summary>
/// Flow #26 ("Reporting - Content &amp; Research top-up"): mirrors
/// <c>UMS.Shared.Faculty.IFacultyReportingQuery</c>'s exact pattern - lives here (not
/// <c>UMS.Modules.Research.*</c>) so Reporting can call it without a forbidden dependency on
/// Research's internals (module-boundaries.md, ADR-0002). Research's own Infrastructure layer
/// registers the one real implementation.
///
/// <para>
/// Replaces the Flow #22 base build's documented "Research" regulatory-report proxy
/// (<c>RegulatoryReportDefinitionCatalogSeeder</c>'s own former remarks: "Faculty's own
/// ResearchProfile output data has no reporting-query contract in this base flow - approximated
/// with faculty headcount pending that contract") - Research (Flow #25) now exists as a real module
/// with its own <c>Grant</c>/<c>Publication</c>/<c>InstitutionalRepositoryEntry</c> aggregates, so
/// this is the genuine contract that proxy was always waiting on.
/// </para>
///
/// <para>
/// Deliberately scoped to plain aggregate counts, never a raw entity row (this codebase's
/// established "a reporting-query contract returns an already-aggregated snapshot, never a live
/// cross-schema join" invariant - reporting requirement-spec.md §4). No <c>asOf</c> parameter - see
/// <c>UMS.Shared.Academic.IAcademicReportingQuery</c>'s own remarks for the documented
/// read-consistency simplification every reporting-query contract in this codebase shares.
/// </para>
/// </summary>
public interface IResearchReportingQuery
{
    /// <summary>Total/active Grant counts, confirmed funding amount by currency, and Publication/InstitutionalRepositoryEntry counts - the whole Research-owned half of the "research-dashboard" DashboardMetric.</summary>
    public Task<ResearchDashboardSnapshot> GetDashboardSnapshotAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// <see cref="ConfirmedFundingAmountByCurrency"/> is a per-currency total, never a single converted
/// figure - Research's own <c>Money</c> value object (design-decisions.md "Grant Funding-Amount
/// Currency Handling") stores an international grant in its original currency with NO automatic FX
/// conversion, so a single summed total across currencies would silently misrepresent the figure.
/// Only Grants that have reached a confirmed-funding status (<c>Funded</c>/<c>Active</c>/
/// <c>Closed</c>/<c>Reported</c>) are counted - a <c>Proposed</c> Grant's funding amount is not yet
/// awarded, and a <c>Rejected</c>/<c>Withdrawn</c> Grant's never was.
/// </summary>
public sealed record ResearchDashboardSnapshot(
    int TotalGrants,
    int TotalActiveGrants,
    IReadOnlyDictionary<string, decimal> ConfirmedFundingAmountByCurrency,
    int TotalPublications,
    int TotalRepositoryEntries);
