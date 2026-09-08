namespace UMS.Shared.Finance;

/// <summary>
/// RPT-3/RPT-6: mirrors <c>UMS.Shared.Academic.IAcademicReportingQuery</c>'s exact pattern - lives
/// here (not <c>UMS.Modules.Finance.*</c>) so Reporting can call it without a forbidden dependency
/// on Finance's internals (module-boundaries.md, ADR-0002). Finance's own Infrastructure layer
/// registers the one real implementation.
///
/// <para>
/// No <c>asOf</c> parameter - see <c>UMS.Shared.Academic.IAcademicReportingQuery</c>'s own remarks
/// for the documented read-consistency simplification. reporting requirement-spec.md §2.2 aligns
/// Finance's dashboard cadence (nightly) to Finance's own daily reconciliation job (ADR-0008) so the
/// two "as of" instants are conceptually close, though this pass does not literally chain the two
/// jobs together - both simply run nightly.
/// </para>
/// </summary>
public interface IFinanceReportingQuery
{
    /// <summary>RPT-6: total/daily/monthly collection, outstanding fees, refunds, failed transactions, open reconciliation exceptions, revenue by fee-type category.</summary>
    public Task<FinanceDashboardSnapshot> GetDashboardSnapshotAsync(CancellationToken cancellationToken = default);
}

/// <summary><see cref="DailyCollection"/>/<see cref="MonthlyCollection"/> are computed against the calling job's own <c>asOf</c>-adjacent wall-clock day/month (UTC) - not calendar-locale-adjusted, a first-pass simplification.</summary>
public sealed record FinanceDashboardSnapshot(
    decimal TotalCollection,
    decimal DailyCollection,
    decimal MonthlyCollection,
    decimal OutstandingFees,
    decimal TotalRefunds,
    int FailedTransactionCount,
    int OpenReconciliationExceptionCount,
    IReadOnlyDictionary<string, decimal> RevenueByCategory);
