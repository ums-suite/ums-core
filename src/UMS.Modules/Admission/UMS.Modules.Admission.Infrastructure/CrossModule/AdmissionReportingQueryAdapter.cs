using Microsoft.EntityFrameworkCore;
using UMS.Modules.Admission.Domain.Applications;
using UMS.Modules.Admission.Domain.ExamAttempts;
using UMS.Modules.Admission.Infrastructure.Persistence;
using UMS.Shared.Admission;

namespace UMS.Modules.Admission.Infrastructure.CrossModule;

/// <summary>
/// RPT-3/RPT-5: the one real implementation of <see cref="IAdmissionReportingQuery"/> - mirrors
/// <c>UMS.Modules.Academic.Infrastructure.CrossModule.AcademicReportingQueryAdapter</c>'s exact
/// pattern. <see cref="IsAnyCampaignActiveAsync"/> is what
/// <c>UMS.Modules.Reporting.Application.DashboardMetrics.AdmissionDashboardRefreshService</c> calls
/// on every tick to self-determine its own next-run interval (edge-cases.md "An admission campaign
/// starts or ends mid-cycle").
///
/// <para><see cref="AdmissionDashboardSnapshot.ApplicationsByProgram"/> attributes each Application
/// to its first-choice Program only (<c>ProgramChoice.Rank == 1</c>) - the same "first-choice-only"
/// simplification <c>MeritList.Generate</c> already establishes as this module's own convention for
/// single-program attribution.</para>
/// </summary>
internal sealed class AdmissionReportingQueryAdapter(AdmissionDbContext context) : IAdmissionReportingQuery
{
    public async Task<AdmissionDashboardSnapshot> GetDashboardSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var totalApplications = await context.Applications.CountAsync(cancellationToken).ConfigureAwait(false);

        var applicationsByProgram = await context.Applications
            .SelectMany(a => a.ProgramChoices.Where(pc => pc.Rank == 1).Select(pc => pc.ProgramId))
            .GroupBy(programId => programId)
            .Select(g => new { ProgramId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.ProgramId, g => g.Count, cancellationToken).ConfigureAwait(false);

        var paidCount = await context.Applications.CountAsync(a => a.IsApplicationFeePaid, cancellationToken).ConfigureAwait(false);
        var paymentCompletionRate = totalApplications > 0 ? Math.Round((decimal)paidCount / totalApplications * 100m, 2) : 0m;

        var examParticipation = await context.ExamAttempts.CountAsync(e => e.Status == ExamAttemptStatus.Submitted, cancellationToken).ConfigureAwait(false);

        var meritOutcomeDistribution = await context.MeritLists
            .SelectMany(m => m.Entries)
            .GroupBy(e => e.Outcome)
            .Select(g => new { Outcome = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Outcome.ToString(), g => g.Count, cancellationToken).ConfigureAwait(false);

        var confirmedCount = await context.Applications.CountAsync(a => a.Status == ApplicationStatus.Confirmed, cancellationToken).ConfigureAwait(false);
        var lockedOrLaterCount = await context.Applications
            .CountAsync(a => a.Status == ApplicationStatus.Locked || a.Status == ApplicationStatus.Confirmed || a.Status == ApplicationStatus.Declined, cancellationToken)
            .ConfigureAwait(false);
        var conversionRate = lockedOrLaterCount > 0 ? Math.Round((decimal)confirmedCount / lockedOrLaterCount * 100m, 2) : 0m;

        return new AdmissionDashboardSnapshot(totalApplications, applicationsByProgram, paymentCompletionRate, examParticipation, meritOutcomeDistribution, conversionRate);
    }

    public async Task<bool> IsAnyCampaignActiveAsync(CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);
        return await context.Campaigns
            .AnyAsync(c => c.ApplicationWindow.Start <= today && c.ApplicationWindow.End >= today, cancellationToken)
            .ConfigureAwait(false);
    }
}
