using Microsoft.EntityFrameworkCore;
using UMS.Modules.Finance.Domain.Invoices;
using UMS.Modules.Finance.Domain.Payments;
using UMS.Modules.Finance.Infrastructure.Persistence;
using UMS.Shared.Finance;

namespace UMS.Modules.Finance.Infrastructure.CrossModule;

/// <summary>
/// RPT-3/RPT-6: the one real implementation of <see cref="IFinanceReportingQuery"/> - mirrors
/// <c>UMS.Modules.Academic.Infrastructure.CrossModule.AcademicReportingQueryAdapter</c>'s exact
/// pattern.
///
/// <para>
/// <b>Query-shape note:</b> <see cref="Payment.Amount"/>/<see cref="Invoice.TotalAmount"/> are
/// <c>ComplexProperty</c>-mapped (see <c>PaymentConfiguration</c>/<c>InvoiceConfiguration</c>'s own
/// remarks) - a mapping this codebase has already found fragile when composed with certain query
/// shapes (raw <c>FromSqlInterpolated</c> root queries, an <c>OwnsMany</c> <c>Include</c>, a
/// <c>HasIndex</c> spanning an owner scalar + nested complex-property member). Rather than risk an
/// unexplained translation failure on a join+group-by shape crossing both Payment and Invoice, the
/// per-category revenue breakdown here uses the SAME "materialize the flat scalars, then aggregate
/// in-memory" two-step discipline this codebase already applies to a ComplexProperty entity's
/// pessimistic-lock queries - a deliberate, documented choice, not an unexamined default.
/// </para>
/// </summary>
internal sealed class FinanceReportingQueryAdapter(FinanceDbContext context) : IFinanceReportingQuery
{
    public async Task<FinanceDashboardSnapshot> GetDashboardSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var successfulPayments = await context.Payments
            .Where(p => p.Status == PaymentStatus.Successful || p.Status == PaymentStatus.Reconciled)
            .Select(p => new { InvoiceId = p.InvoiceId.Value, Amount = p.Amount.Amount, p.CreatedAt })
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var invoiceFeeTypes = await context.Invoices
            .Select(i => new { Id = i.Id.Value, i.FeeType })
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var feeTypeByInvoiceId = invoiceFeeTypes.ToDictionary(i => i.Id, i => i.FeeType);

        var totalCollection = successfulPayments.Sum(p => p.Amount);

        var utcNow = DateTimeOffset.UtcNow;
        var todayStart = new DateTimeOffset(utcNow.Year, utcNow.Month, utcNow.Day, 0, 0, 0, TimeSpan.Zero);
        var monthStart = new DateTimeOffset(utcNow.Year, utcNow.Month, 1, 0, 0, 0, TimeSpan.Zero);

        var dailyCollection = successfulPayments.Where(p => p.CreatedAt >= todayStart).Sum(p => p.Amount);
        var monthlyCollection = successfulPayments.Where(p => p.CreatedAt >= monthStart).Sum(p => p.Amount);

        var revenueByCategory = successfulPayments
            .GroupBy(p => feeTypeByInvoiceId.GetValueOrDefault(p.InvoiceId, "Unknown"))
            .ToDictionary(g => g.Key, g => g.Sum(p => p.Amount));

        var outstandingFees = await context.Invoices
            .Where(i => i.Status == InvoiceStatus.Open)
            .SumAsync(i => i.TotalAmount.Amount, cancellationToken).ConfigureAwait(false);

        var totalRefunds = await context.Payments
            .SelectMany(p => p.Refunds)
            .Where(r => r.Status == RefundStatus.Succeeded)
            .SumAsync(r => r.Amount, cancellationToken).ConfigureAwait(false);

        var failedTransactionCount = await context.Payments.CountAsync(p => p.Status == PaymentStatus.Failed, cancellationToken).ConfigureAwait(false);

        // No resolution-tracking field exists yet on ReconciliationException in this base flow -
        // every recorded exception counts as "open" (a documented simplification).
        var openReconciliationExceptionCount = await context.ReconciliationExceptions.CountAsync(cancellationToken).ConfigureAwait(false);

        return new FinanceDashboardSnapshot(
            totalCollection,
            dailyCollection,
            monthlyCollection,
            outstandingFees,
            totalRefunds,
            failedTransactionCount,
            openReconciliationExceptionCount,
            revenueByCategory);
    }
}
