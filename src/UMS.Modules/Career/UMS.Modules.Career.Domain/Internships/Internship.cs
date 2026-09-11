using UMS.Modules.Career.Domain.Common;
using UMS.Modules.Career.Domain.Events;

namespace UMS.Modules.Career.Domain.Internships;

/// <summary>
/// CAR-2/CAR-3/CAR-4/CAR-9: the `Internship` aggregate root (requirement-spec.md §2.2, §3, §4).
/// Lifecycle `Draft -> Published -> ApplicationsOpen -> ApplicationsClosed`, `Withdrawn` reachable
/// from any non-terminal state.
///
/// <para>
/// <see cref="EligibilityCriteria"/> is stored as three plain scalar columns (never a
/// <c>ComplexProperty</c> - ums-core-gotchas: a <c>ComplexProperty</c>-mapped value object with a
/// collection member does not compose cleanly with the module's own EF configuration conventions)
/// and exposed as a computed value-object property.
/// </para>
/// </summary>
public sealed class Internship : AggregateRoot<InternshipId>
{
    private List<Guid> _eligibilityProgramIds = [];

    private Internship()
    {
    }

    private Internship(InternshipId id, Guid employerProfileId, string title, string description, string location, string? stipendNote, DateTimeOffset applicationDeadline, EligibilityCriteria eligibility, DateTimeOffset now)
    {
        Id = id;
        EmployerProfileId = employerProfileId;
        Title = title;
        Description = description;
        Location = location;
        StipendNote = stipendNote;
        ApplicationDeadline = applicationDeadline;
        SetEligibility(eligibility);
        Status = InternshipStatus.Draft;
        CreatedAt = now;
    }

    public Guid EmployerProfileId { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public string Location { get; private set; } = string.Empty;

    public string? StipendNote { get; private set; }

    public DateTimeOffset ApplicationDeadline { get; private set; }

    public InternshipStatus Status { get; private set; }

    public string? WithdrawalReason { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? PublishedAt { get; private set; }

    public decimal? EligibilityMinCgpa { get; private set; }

    public int? EligibilityMinYearOfStudy { get; private set; }

    public IReadOnlyCollection<Guid> EligibilityProgramIds => _eligibilityProgramIds.AsReadOnly();

    public EligibilityCriteria Eligibility => new(_eligibilityProgramIds.AsReadOnly(), EligibilityMinCgpa, EligibilityMinYearOfStudy);

    public static Internship Create(Guid employerProfileId, string title, string description, string location, string? stipendNote, DateTimeOffset applicationDeadline, EligibilityCriteria eligibility, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("An Internship's title is required.", nameof(title));
        }

        if (applicationDeadline <= now)
        {
            throw new ArgumentException("An Internship's application deadline must be in the future.", nameof(applicationDeadline));
        }

        return new Internship(InternshipId.New(), employerProfileId, title.Trim(), description?.Trim() ?? string.Empty, location?.Trim() ?? string.Empty, stipendNote?.Trim(), applicationDeadline, eligibility, now);
    }

    /// <summary>CAR-2 `PATCH /internships/{id}` - permitted only while still `Draft` (a Published posting's terms should not silently change under Students who already saw/applied to it).</summary>
    public void Edit(string title, string description, string location, string? stipendNote, DateTimeOffset applicationDeadline, EligibilityCriteria eligibility, DateTimeOffset now)
    {
        if (Status != InternshipStatus.Draft)
        {
            throw new InvalidOperationException($"Cannot edit an Internship in status {Status} - only a Draft posting may be edited.");
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("An Internship's title is required.", nameof(title));
        }

        if (applicationDeadline <= now)
        {
            throw new ArgumentException("An Internship's application deadline must be in the future.", nameof(applicationDeadline));
        }

        Title = title.Trim();
        Description = description?.Trim() ?? string.Empty;
        Location = location?.Trim() ?? string.Empty;
        StipendNote = stipendNote?.Trim();
        ApplicationDeadline = applicationDeadline;
        SetEligibility(eligibility);
    }

    public void Publish(DateTimeOffset now)
    {
        if (Status != InternshipStatus.Draft)
        {
            throw new InvalidOperationException($"Cannot publish an Internship in status {Status} - only a Draft posting may be published.");
        }

        Status = InternshipStatus.Published;
        PublishedAt = now;
        Raise(new InternshipPublished(Id.Value, now));
    }

    /// <summary>requirement-spec.md §2.2: staff action, or scheduled if a start date was set (the scheduling itself is out of this aggregate's own concern - the caller decides when to invoke this).</summary>
    public void OpenApplications()
    {
        if (Status != InternshipStatus.Published)
        {
            throw new InvalidOperationException($"Cannot open applications for an Internship in status {Status} - only a Published posting may open.");
        }

        Status = InternshipStatus.ApplicationsOpen;
    }

    /// <summary>CAR-3: the scheduled deadline sweep - idempotent-by-construction, only an ApplicationsOpen posting past its own deadline transitions.</summary>
    public bool CloseApplicationsIfDue(DateTimeOffset now)
    {
        if (Status != InternshipStatus.ApplicationsOpen || ApplicationDeadline > now)
        {
            return false;
        }

        Status = InternshipStatus.ApplicationsClosed;
        Raise(new InternshipApplicationsClosed(Id.Value, now));
        return true;
    }

    /// <summary>An explicit staff action closing applications ahead of the stated deadline.</summary>
    public void CloseApplications(DateTimeOffset now)
    {
        if (Status != InternshipStatus.ApplicationsOpen)
        {
            throw new InvalidOperationException($"Cannot close applications for an Internship in status {Status} - only an ApplicationsOpen posting may close.");
        }

        Status = InternshipStatus.ApplicationsClosed;
        Raise(new InternshipApplicationsClosed(Id.Value, now));
    }

    /// <summary>requirement-spec.md §2.6: withdrawable from any non-terminal state, by staff (acting for the employer, since no employer login exists) or by staff for policy reasons.</summary>
    public void Withdraw(string reason)
    {
        if (Status is InternshipStatus.ApplicationsClosed or InternshipStatus.Withdrawn)
        {
            throw new InvalidOperationException($"Cannot withdraw an Internship in status {Status} - it is already terminal.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("A withdrawal reason is required.", nameof(reason));
        }

        Status = InternshipStatus.Withdrawn;
        WithdrawalReason = reason.Trim();
        Raise(new InternshipWithdrawn(Id.Value, WithdrawalReason, DateTimeOffset.UtcNow));
    }

    /// <summary>requirement-spec.md §4: "An Internship past ApplicationsClosed/Withdrawn cannot be applied to - enforced at write time." Used only for pre-flight UX; the REAL enforcement is the application layer's own guarded-insert (edge-cases.md).</summary>
    public bool AcceptsApplications(DateTimeOffset now) => Status == InternshipStatus.ApplicationsOpen && ApplicationDeadline > now;

    /// <summary>requirement-spec.md §2.2: only Published/ApplicationsOpen postings are browsable.</summary>
    public bool IsBrowsable() => Status is InternshipStatus.Published or InternshipStatus.ApplicationsOpen;

    private void SetEligibility(EligibilityCriteria eligibility)
    {
        _eligibilityProgramIds = eligibility.ProgramIds.ToList();
        EligibilityMinCgpa = eligibility.MinCgpa;
        EligibilityMinYearOfStudy = eligibility.MinYearOfStudy;
    }
}
