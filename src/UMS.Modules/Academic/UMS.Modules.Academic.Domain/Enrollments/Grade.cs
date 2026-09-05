using UMS.Shared.Domain;

namespace UMS.Modules.Academic.Domain.Enrollments;

/// <summary>
/// requirement-spec.md §9 decision 2: modeled as a child entity of <see cref="Enrollment"/>, NOT
/// its own aggregate root - one Grade per completed Enrollment. Grade's own mutability past the
/// Faculty-submission stage is gated entirely by the parent CourseOffering's `ResultPublication`
/// state (checked/enforced by <c>GradeService</c>/<c>GradeRepository</c> at the application/
/// infrastructure layer via the state-guarded conditional update - see
/// <c>ResultPublication</c>'s own remarks) - this entity itself carries no independent lock/status
/// field of its own, deliberately, since requirement-spec.md §9 decision 2 ties "who can grade
/// what" to the owning Enrollment's own scope rather than a second, parallel state machine.
/// </summary>
public sealed class Grade
{
    private readonly List<AssessmentScoreEntry> _scores = [];
    private readonly List<GradeCorrectionEntry> _corrections = [];

    internal Grade(GradeId id, EnrollmentId enrollmentId)
    {
        Id = id;
        EnrollmentId = enrollmentId;
    }

    private Grade()
    {
    }

    public GradeId Id { get; private set; }

    public EnrollmentId EnrollmentId { get; private set; }

    public PercentageOrGpa? CalculatedScore { get; private set; }

    public string? LetterGrade { get; private set; }

    public DateTimeOffset? SubmittedAt { get; private set; }

    public Guid? SubmittedByUserId { get; private set; }

    public IReadOnlyCollection<AssessmentScoreEntry> Scores => _scores.AsReadOnly();

    public IReadOnlyCollection<GradeCorrectionEntry> Corrections => _corrections.AsReadOnly();

    /// <summary>ACD-6's prerequisite gate: a submitted Grade counts as satisfying a prerequisite once it carries any letter grade other than the standard failing grade ('F'). A never-submitted Grade (<see cref="LetterGrade"/> null) never counts.</summary>
    public bool IsPassing => LetterGrade is not null && !string.Equals(LetterGrade, "F", StringComparison.OrdinalIgnoreCase);

    /// <summary>ACD-10: Faculty submits/resubmits marks. Callable repeatedly while the parent ResultPublication is `Draft`/`Calculated` - the caller (<c>GradeService</c>) is responsible for enforcing that state guard against the DB before invoking this.</summary>
    public void Submit(IReadOnlyCollection<(Guid AssessmentId, decimal Score)> scores, PercentageOrGpa calculatedScore, string letterGrade, Guid submittedByUserId, DateTimeOffset now)
    {
        _scores.Clear();
        foreach (var (assessmentId, score) in scores)
        {
            _scores.Add(new AssessmentScoreEntry(Id, assessmentId, score));
        }

        CalculatedScore = calculatedScore;
        LetterGrade = letterGrade;
        SubmittedAt = now;
        SubmittedByUserId = submittedByUserId;
    }

    /// <summary>
    /// ACD-13: the controlled correction workflow's actual value mutation - the caller
    /// (<c>GradeCorrectionService</c>) is responsible for having already re-entered the parent
    /// ResultPublication at `Verified` via the state-guarded conditional update BEFORE invoking
    /// this, and for recording the resulting <c>GradeCorrected</c> event.
    ///
    /// <para>
    /// Replaces <see cref="Scores"/> the same way <see cref="Submit"/> does - a genuine bug caught
    /// during this flow's manual end-to-end verification found this method originally updated only
    /// the derived <see cref="CalculatedScore"/>/<see cref="LetterGrade"/> fields, leaving the
    /// per-assessment <see cref="AssessmentScoreEntry"/> rows a correction is meant to fix (e.g.
    /// "transcription error in original marking") permanently stale - the very data a correction
    /// exists to fix would otherwise survive the correction unchanged, silently diverging from the
    /// now-correct aggregate it's supposed to justify.
    /// </para>
    /// </summary>
    public decimal Correct(IReadOnlyCollection<(Guid AssessmentId, decimal Score)> scores, PercentageOrGpa newScore, string newLetterGrade, string reason, Guid correctedByUserId, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("A correction reason is mandatory (ums-requirements.md §4.1 audit requirement).", nameof(reason));
        }

        var previousScore = CalculatedScore?.Value ?? 0;
        _corrections.Add(new GradeCorrectionEntry(Id, previousScore, newScore.Value, reason.Trim(), correctedByUserId, now));

        _scores.Clear();
        foreach (var (assessmentId, score) in scores)
        {
            _scores.Add(new AssessmentScoreEntry(Id, assessmentId, score));
        }

        CalculatedScore = newScore;
        LetterGrade = newLetterGrade;
        return previousScore;
    }
}
