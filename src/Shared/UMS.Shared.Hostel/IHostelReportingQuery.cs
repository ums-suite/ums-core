namespace UMS.Shared.Hostel;

/// <summary>
/// RPT-3/RPT-8: mirrors <c>UMS.Shared.Academic.IAcademicReportingQuery</c>'s exact pattern - lives
/// here (not <c>UMS.Modules.Hostel.*</c>) so Reporting can call it without a forbidden dependency
/// on Hostel's internals (module-boundaries.md, ADR-0002). Hostel's own Infrastructure layer
/// registers the one real implementation.
///
/// <para>
/// No <c>asOf</c> parameter - see <c>UMS.Shared.Academic.IAcademicReportingQuery</c>'s own remarks
/// for the documented read-consistency simplification. Out of scope per reporting requirement-spec.md
/// §1: "any real-time operational read (e.g., 'is this bed available right now')" - this contract
/// only ever answers Reporting's own scheduled hourly pull, never a live availability check (that
/// stays with Hostel's own endpoints directly).
/// </para>
/// </summary>
public interface IHostelReportingQuery
{
    /// <summary>RPT-8: total/occupied/available beds, occupancy percentage, pending applications, hostel-fee figures.</summary>
    public Task<HostelDashboardSnapshot> GetDashboardSnapshotAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// <see cref="HostelFeePaidAllocationCount"/>/<see cref="HostelFeePendingAllocationCount"/> report
/// fee status as a COUNT of Allocations, not a currency amount - Hostel's own schema tracks only
/// <c>Allocation.FeePaidAt</c>/<c>InvoiceId</c>, never the fee's actual amount (that lives in
/// Finance, outside Hostel's own ownership - ADR-0002). A currency-amount view of hostel-fee
/// collection is already covered by the Financial dashboard's own <c>RevenueByCategory</c>
/// breakdown (RPT-6) - this is a documented first-pass simplification, not a missing figure.
/// </summary>
public sealed record HostelDashboardSnapshot(
    int TotalBeds,
    int OccupiedBeds,
    int AvailableBeds,
    decimal OccupancyPercentage,
    int PendingApplications,
    int HostelFeePaidAllocationCount,
    int HostelFeePendingAllocationCount);
