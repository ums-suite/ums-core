namespace UMS.Shared.Admission;

/// <summary>
/// RPT-3/RPT-5: mirrors <c>UMS.Shared.Academic.IAcademicReportingQuery</c>'s exact pattern - lives
/// here (not <c>UMS.Modules.Admission.*</c>) so Reporting can call it without a forbidden
/// dependency on Admission's internals (module-boundaries.md, ADR-0002). Admission's own
/// Infrastructure layer registers the one real implementation.
///
/// <para>
/// No <c>asOf</c> parameter - see <c>IAcademicReportingQuery</c>'s own remarks for the documented
/// read-consistency simplification this first pass adopts uniformly across every reporting query
/// contract.
/// </para>
/// </summary>
public interface IAdmissionReportingQuery
{
    /// <summary>RPT-5: applications (total/by program), payment completion rate, exam participation, merit-outcome distribution, admission conversion.</summary>
    public Task<AdmissionDashboardSnapshot> GetDashboardSnapshotAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// edge-cases.md "An admission campaign starts or ends mid-cycle": the Admission dashboard's own
    /// <c>MetricRefreshJob</c> calls this on every tick to self-determine its own next-run interval
    /// (near-real-time vs. nightly) - never a manually-toggled ops setting.
    /// </summary>
    public Task<bool> IsAnyCampaignActiveAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// RPT-5's Admission dashboard read model. <see cref="ApplicationsByProgram"/> counts by each
/// Application's first program choice (Admission's own <c>MeritList.Generate</c> already
/// establishes "first-choice-only" as this module's own simplification convention for
/// single-program attribution - reused here for the same reason). <see cref="MeritOutcomeDistribution"/>
/// is the merit-distribution figure requirement-spec.md §2.2 names, keyed by each MeritListEntry's
/// own <c>MeritOutcome</c> name (e.g. "Admitted"/"Waitlisted"/"NotAdmitted").
/// </summary>
public sealed record AdmissionDashboardSnapshot(
    int TotalApplications,
    IReadOnlyDictionary<Guid, int> ApplicationsByProgram,
    decimal PaymentCompletionRate,
    int ExamParticipation,
    IReadOnlyDictionary<string, int> MeritOutcomeDistribution,
    decimal ConversionRate);
