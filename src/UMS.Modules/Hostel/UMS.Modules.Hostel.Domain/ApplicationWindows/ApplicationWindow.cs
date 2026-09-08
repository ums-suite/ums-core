using UMS.Modules.Hostel.Domain.Common;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Hostel.Domain.ApplicationWindows;

/// <summary>
/// HOS-2: an admin-configured application window for one Hostel Officer-defined session
/// (requirement-spec.md §2 step 1: "start/end timestamps, eligible programs/years"). Deliberately its
/// own, Hostel-local session concept - NOT a reference to Academic's own <c>AcademicSession</c>
/// (module-boundaries.md: Hostel depends only on Identity/Student/Finance, never Academic), the same
/// reasoning requirement-spec.md §9 decision 1 already applies to Building/Room.
///
/// <para>
/// <see cref="EligibleProgramIds"/> holds Program ids opaquely (a plain <see cref="Guid"/>, never
/// Organization's own <c>ProgramId</c> type) for the same reason Finance's own
/// <c>FeeApplicability.ReferenceId</c> does - Hostel has no outgoing dependency on Organization
/// either. An empty list means "no Program restriction" (every Program eligible); likewise an empty
/// <see cref="EligibleYears"/> means "no year-of-study restriction."
/// </para>
/// </summary>
public sealed class ApplicationWindow : AggregateRoot<ApplicationWindowId>
{
    private readonly List<Guid> _eligibleProgramIds = [];
    private readonly List<int> _eligibleYears = [];
    private readonly List<EligibilityRuleDefinition> _eligibilityRules = [];

    private ApplicationWindow()
    {
    }

    private ApplicationWindow(ApplicationWindowId id, string sessionLabel, DateTimeOffset opensAt, DateTimeOffset closesAt, DateTimeOffset now)
    {
        Id = id;
        SessionLabel = sessionLabel;
        OpensAt = opensAt;
        ClosesAt = closesAt;
        RulesVersion = 1;
        CreatedAt = now;
    }

    public string SessionLabel { get; private set; } = string.Empty;

    public DateTimeOffset OpensAt { get; private set; }

    public DateTimeOffset ClosesAt { get; private set; }

    public IReadOnlyCollection<Guid> EligibleProgramIds => _eligibleProgramIds.AsReadOnly();

    public IReadOnlyCollection<int> EligibleYears => _eligibleYears.AsReadOnly();

    public IReadOnlyCollection<EligibilityRuleDefinition> EligibilityRules => _eligibilityRules.AsReadOnly();

    /// <summary>requirement-spec.md §9 decision 4: "a configurable, versioned rule set" - increments every time <see cref="ReplaceEligibilityRules"/> is called.</summary>
    public int RulesVersion { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static Result<ApplicationWindow> Create(string sessionLabel, DateTimeOffset opensAt, DateTimeOffset closesAt, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(sessionLabel))
        {
            return Error.Validation("application_window.session_label_required", "An ApplicationWindow's session label is required.");
        }

        if (closesAt <= opensAt)
        {
            return Error.Validation("application_window.invalid_range", "An ApplicationWindow's closesAt must be after its opensAt.");
        }

        return new ApplicationWindow(ApplicationWindowId.New(), sessionLabel.Trim(), opensAt, closesAt, now);
    }

    public Result ReplaceEligiblePrograms(IReadOnlyCollection<Guid> programIds)
    {
        _eligibleProgramIds.Clear();
        _eligibleProgramIds.AddRange(programIds.Distinct());
        return Result.Success();
    }

    public Result ReplaceEligibleYears(IReadOnlyCollection<int> years)
    {
        if (years.Any(y => y <= 0))
        {
            return Result.Failure(Error.Validation("application_window.invalid_year", "Every eligible year of study must be a positive number."));
        }

        _eligibleYears.Clear();
        _eligibleYears.AddRange(years.Distinct().OrderBy(y => y));
        return Result.Success();
    }

    public void ReplaceEligibilityRules(IReadOnlyCollection<EligibilityRuleDefinition> rules)
    {
        _eligibilityRules.Clear();
        _eligibilityRules.AddRange(rules);
        RulesVersion++;
    }

    public bool IsOpenAt(DateTimeOffset asOf) => asOf >= OpensAt && asOf < ClosesAt;

    public bool AllowsProgram(Guid programId) => _eligibleProgramIds.Count == 0 || _eligibleProgramIds.Contains(programId);

    public bool AllowsYearOfStudy(int yearOfStudy) => _eligibleYears.Count == 0 || _eligibleYears.Contains(yearOfStudy);
}
