using UMS.Shared.Domain;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Admission.Domain.Campaigns;

/// <summary>
/// requirement-spec.md §2/§3: a value object nested inside <see cref="AdmissionCampaign"/>, evaluated
/// both at an <see cref="Applications.Application"/>'s submit (against at least one chosen Program)
/// and again at <see cref="MeritLists.MeritList"/> generation. Reuses <see cref="PercentageOrGpa"/>
/// (scaffolded by Academic, Flow #12, explicitly "every later module reuses this instead of
/// re-implementing its own bounded-score type") for <see cref="MinimumScore"/>.
/// </summary>
public sealed record EligibilityRule
{
    private EligibilityRule(Guid programId, PercentageOrGpa minimumScore, string? requiredBoard)
    {
        ProgramId = programId;
        MinimumScore = minimumScore;
        RequiredBoard = requiredBoard;
    }

    // EF Core materialization only (PropertyAccessMode.Field) - never called from application code.
    private EligibilityRule()
    {
    }

    public Guid ProgramId { get; }

    public PercentageOrGpa MinimumScore { get; } = null!;

    /// <summary>Optional - a specific examining board name (e.g. <c>"Dhaka Board"</c>) the applicant's prior academic record must match; <c>null</c> means any board is acceptable.</summary>
    public string? RequiredBoard { get; }

    public static Result<EligibilityRule> Create(Guid programId, PercentageOrGpa minimumScore, string? requiredBoard)
    {
        if (programId == Guid.Empty)
        {
            return Error.Validation("eligibility_rule.program_id_required", "An EligibilityRule's programId is required.");
        }

        ArgumentNullException.ThrowIfNull(minimumScore);
        return new EligibilityRule(programId, minimumScore, string.IsNullOrWhiteSpace(requiredBoard) ? null : requiredBoard.Trim());
    }

    /// <summary>Applied at both submit-time (§2) and merit-list-generation-time (§2/§4) against one <see cref="Applicants.AcademicRecord"/> the applicant declared for the chosen Program.</summary>
    public bool IsSatisfiedBy(Applicants.AcademicRecord record)
    {
        if (RequiredBoard is not null && !string.Equals(record.Board, RequiredBoard, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Comparable only within the same scale (ums-conventions.md: PercentageOrGpa is
        // "scale-aware" precisely so a Percentage rule is never silently compared against a Gpa
        // record) - a scale mismatch is treated as not-satisfied rather than throwing, since a
        // legitimate applicant may hold a qualification on a different scale than this specific
        // rule targets.
        return record.Score.Scale == MinimumScore.Scale && record.Score.Value >= MinimumScore.Value;
    }
}
