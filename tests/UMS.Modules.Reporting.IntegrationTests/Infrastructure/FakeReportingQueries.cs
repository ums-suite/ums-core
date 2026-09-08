using UMS.Shared.Academic;
using UMS.Shared.Admission;
using UMS.Shared.Content;
using UMS.Shared.Faculty;
using UMS.Shared.Finance;
using UMS.Shared.Hostel;
using UMS.Shared.Library;
using UMS.Shared.Research;

namespace UMS.Modules.Reporting.IntegrationTests.Infrastructure;

/// <summary>
/// This suite tests Reporting's OWN mechanics (the Redis lease, the DashboardMetric/
/// RegulatoryReportRun persistence and state machines, the RegulatoryReportRun execution pipeline)
/// against a real Postgres + Redis (Testcontainers) - Academic/Admission/Finance/Faculty/Hostel/
/// Library are genuinely different modules with their own already-verified reporting-query
/// implementations, so faking those six read-only contracts here keeps this suite's own fixture
/// focused on what it actually exercises, mirroring Admission's own <c>FakeCrossModuleAdapters</c>
/// posture exactly. Every field is mutable so an individual test can steer exactly what a dashboard
/// refresh computes.
/// </summary>
public sealed class FakeAcademicReportingQuery : IAcademicReportingQuery
{
    public int CallCount { get; private set; }

    public TimeSpan Delay { get; set; } = TimeSpan.Zero;

    public AcademicDashboardSnapshot Snapshot { get; set; } = new(500, 480, 10, 200, 50, new Dictionary<string, int> { ["A"] = 300, ["F"] = 20 }, 94m, 2m, 200, []);

    public AcademicTeachingAggregateSnapshot TeachingAggregates { get; set; } = new(new Dictionary<Guid, int>(), 90m, new Dictionary<string, int>(), 88m);

    public async Task<AcademicDashboardSnapshot> GetDashboardSnapshotAsync(CancellationToken cancellationToken = default)
    {
        CallCount++;
        if (Delay > TimeSpan.Zero)
        {
            await Task.Delay(Delay, cancellationToken).ConfigureAwait(false);
        }

        return Snapshot;
    }

    public Task<AcademicTeachingAggregateSnapshot> GetTeachingAggregatesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(TeachingAggregates);
}

public sealed class FakeAdmissionReportingQuery : IAdmissionReportingQuery
{
    public AdmissionDashboardSnapshot Snapshot { get; set; } = new(1000, new Dictionary<Guid, int>(), 85m, 900, new Dictionary<string, int> { ["Admitted"] = 400 }, 40m);

    public bool AnyCampaignActive { get; set; }

    public Task<AdmissionDashboardSnapshot> GetDashboardSnapshotAsync(CancellationToken cancellationToken = default) => Task.FromResult(Snapshot);

    public Task<bool> IsAnyCampaignActiveAsync(CancellationToken cancellationToken = default) => Task.FromResult(AnyCampaignActive);
}

public sealed class FakeFinanceReportingQuery : IFinanceReportingQuery
{
    public FinanceDashboardSnapshot Snapshot { get; set; } = new(1_000_000m, 5000m, 150_000m, 20_000m, 3000m, 2, 1, new Dictionary<string, decimal> { ["Tuition"] = 900_000m });

    public Task<FinanceDashboardSnapshot> GetDashboardSnapshotAsync(CancellationToken cancellationToken = default) => Task.FromResult(Snapshot);
}

public sealed class FakeFacultyReportingQuery : IFacultyReportingQuery
{
    public FacultyDashboardSnapshot Snapshot { get; set; } = new(150, new Dictionary<string, int> { ["Active"] = 140 }, 3, 5, 4.5m);

    public Task<FacultyDashboardSnapshot> GetDashboardSnapshotAsync(CancellationToken cancellationToken = default) => Task.FromResult(Snapshot);
}

public sealed class FakeHostelReportingQuery : IHostelReportingQuery
{
    public HostelDashboardSnapshot Snapshot { get; set; } = new(1000, 800, 200, 80m, 15, 750, 50);

    public Task<HostelDashboardSnapshot> GetDashboardSnapshotAsync(CancellationToken cancellationToken = default) => Task.FromResult(Snapshot);
}

public sealed class FakeLibraryReportingQuery : ILibraryReportingQuery
{
    public LibraryDashboardSnapshot Snapshot { get; set; } = new(5000, 6000, 1200, 45, 3200m, []);

    public Task<LibraryDashboardSnapshot> GetDashboardSnapshotAsync(CancellationToken cancellationToken = default) => Task.FromResult(Snapshot);
}

/// <summary>Flow #26: fakes the real <c>ResearchReportingQueryAdapter</c> - see this class's own file remarks for why Research's/Content's real implementations are exercised against a real Postgres instead, in their own module's IntegrationTests suite.</summary>
public sealed class FakeResearchReportingQuery : IResearchReportingQuery
{
    public ResearchDashboardSnapshot Snapshot { get; set; } = new(40, 15, new Dictionary<string, decimal> { ["USD"] = 500_000m, ["BDT"] = 2_000_000m }, 120, 30);

    public Task<ResearchDashboardSnapshot> GetDashboardSnapshotAsync(CancellationToken cancellationToken = default) => Task.FromResult(Snapshot);
}

/// <summary>Flow #26: fakes the real <c>ContentReportingQueryAdapter</c> - the seventh, Content-owned admin dashboard (a deliberate scope extension, see <c>UMS.Shared.Content.IContentReportingQuery</c>'s own remarks).</summary>
public sealed class FakeContentReportingQuery : IContentReportingQuery
{
    public ContentDashboardSnapshot Snapshot { get; set; } = new(25, 4, 8, 60);

    public Task<ContentDashboardSnapshot> GetDashboardSnapshotAsync(CancellationToken cancellationToken = default) => Task.FromResult(Snapshot);
}
