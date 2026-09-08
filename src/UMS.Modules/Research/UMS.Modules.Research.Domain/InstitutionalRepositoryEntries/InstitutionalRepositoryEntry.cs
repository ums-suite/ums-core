using UMS.Modules.Research.Domain.Common;
using UMS.Modules.Research.Domain.Events;

namespace UMS.Modules.Research.Domain.InstitutionalRepositoryEntries;

/// <summary>
/// RES-10/RES-11/RES-12: the InstitutionalRepositoryEntry aggregate root (requirement-spec.md §2
/// Institutional Repository, §3, §9). A thesis, dissertation, or other scholarly work deposited in
/// the university's repository - never a <c>Research -&gt; Student</c> reference (see
/// <see cref="Contributor"/>'s own remarks).
/// </summary>
public sealed class InstitutionalRepositoryEntry : AggregateRoot<InstitutionalRepositoryEntryId>
{
    private InstitutionalRepositoryEntry()
    {
    }

    private InstitutionalRepositoryEntry(InstitutionalRepositoryEntryId id, string title, RepositoryWorkType workType, Contributor depositor, Guid? supervisingFacultyMemberId, DateOnly depositDate, EmbargoPolicy embargo, DateTimeOffset now)
    {
        Id = id;
        Title = title;
        WorkType = workType;
        Depositor = depositor;
        SupervisingFacultyMemberId = supervisingFacultyMemberId;
        DepositDate = depositDate;
        Embargo = embargo;
        CreatedAt = now;
    }

    public string Title { get; private set; } = string.Empty;

    public RepositoryWorkType WorkType { get; private set; }

    public Contributor Depositor { get; private set; } = null!;

    public Guid? SupervisingFacultyMemberId { get; private set; }

    public DateOnly DepositDate { get; private set; }

    public EmbargoPolicy Embargo { get; private set; } = null!;

    public Guid? ArtifactId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static InstitutionalRepositoryEntry Deposit(string title, RepositoryWorkType workType, Contributor depositor, Guid? supervisingFacultyMemberId, DateOnly depositDate, EmbargoPolicy embargo, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("An InstitutionalRepositoryEntry's title is required.", nameof(title));
        }

        ArgumentNullException.ThrowIfNull(depositor);
        ArgumentNullException.ThrowIfNull(embargo);

        var entry = new InstitutionalRepositoryEntry(InstitutionalRepositoryEntryId.New(), title.Trim(), workType, depositor, supervisingFacultyMemberId, depositDate, embargo, now);
        entry.Raise(new InstitutionalRepositoryEntryDeposited(entry.Id.Value, now));
        return entry;
    }

    public void AttachArtifact(Guid artifactId) => ArtifactId = artifactId;

    /// <summary>design-decisions.md "InstitutionalRepositoryEntry Embargo-Lift Mechanism": called identically by the daily ADR-0014 scheduled worker (automatic, lapsed <c>EmbargoEndDate</c>) and the explicit Admin early-override endpoint - same event either way. A no-op (not an error) when already non-embargoed, so the worker's own redundant sweep tick is harmless.</summary>
    public void LiftEmbargo(DateTimeOffset now)
    {
        if (!Embargo.IsEmbargoed)
        {
            return;
        }

        Embargo = Embargo.Lift();
        Raise(new InstitutionalRepositoryEntryEmbargoLifted(Id.Value, now));
    }
}
