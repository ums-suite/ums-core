using UMS.Modules.Admission.Domain.Common;
using UMS.Modules.Admission.Domain.Events;
using UMS.Modules.Admission.Domain.Tests;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Admission.Domain.ExamAttempts;

/// <summary>
/// ADM-11..14: one Applicant's single, lockable attempt at an AdmissionTest (docs/ddd/ubiquitous-
/// language.md) - requirement-spec.md §4's single-submission invariant: "no further answer
/// mutation is possible under any code path, including an admin-privileged one" once locked.
///
/// <para>
/// <b>Submit/lock concurrency mechanism - read this before changing <see cref="Submit"/>.</b>
/// design-decisions.md "ExamAttempt Locking &amp; Single-Submission Mechanism": a manual submit, a
/// retried/double-click submit, and the server's own timeout-sweep auto-submit all converge on the
/// identical state-guarded conditional <c>UPDATE exam_attempts SET status = 'Submitted', ... WHERE
/// id = @id AND status = 'InProgress'</c> (<c>ExamAttemptRepository.TryLockAsync</c>) - whichever
/// commits first wins. <see cref="Submit"/> itself only mutates fields for the caller that ALREADY
/// won that race (mirrors <see cref="Applications.Application.Lock"/>'s own identical posture); it
/// is never, on its own, what resolves the race.
/// </para>
///
/// <para>
/// <see cref="SaveAnswer"/>'s own in-memory <c>InProgress</c> guard closes edge-cases.md's residual
/// note on the double-submit race: "a slow in-flight auto-save could otherwise land after a lock
/// and silently resurrect an answer" - the repository's own auto-save write path re-asserts the
/// identical <c>WHERE status = 'InProgress'</c> guard at the SQL layer for the same reason.
/// </para>
/// </summary>
public sealed class ExamAttempt : AggregateRoot<ExamAttemptId>
{
    private readonly List<QuestionId> _selectedQuestionIds = [];
    private readonly List<ExamAnswer> _answers = [];
    private readonly List<IntegrityFlag> _integrityFlags = [];

    private ExamAttempt()
    {
    }

    private ExamAttempt(ExamAttemptId id, Guid applicantId, Guid admissionTestId, Guid testSlotId, string rollNumber, long seedValue, IReadOnlyList<QuestionId> selectedQuestionIds, DateTimeOffset startedAt, DateTimeOffset expiresAt)
    {
        Id = id;
        ApplicantId = applicantId;
        AdmissionTestId = admissionTestId;
        TestSlotId = testSlotId;
        RollNumber = rollNumber;
        SeedValue = seedValue;
        _selectedQuestionIds.AddRange(selectedQuestionIds);
        Status = ExamAttemptStatus.InProgress;
        StartedAt = startedAt;
        ExpiresAt = expiresAt;
        EvaluationStatus = EvaluationStatus.Pending;
    }

    public Guid ApplicantId { get; private set; }

    public Guid AdmissionTestId { get; private set; }

    public Guid TestSlotId { get; private set; }

    public string RollNumber { get; private set; } = string.Empty;

    /// <summary>ADR-0018: the <c>IProctoringProvider</c> session handle for this attempt - <c>null</c> if identity verification was skipped/unavailable at start (never a silent auto-pass presented as verified).</summary>
    public string? ProctoringSessionId { get; private set; }

    /// <summary>requirement-spec.md §9 decision 4: logged for audit/appeal defensibility - a later appeal re-derives this attempt's exact question selection from <see cref="Tests.AdmissionTest.SelectQuestions"/> given this same seed.</summary>
    public long SeedValue { get; private set; }

    public IReadOnlyCollection<QuestionId> SelectedQuestionIds => _selectedQuestionIds.AsReadOnly();

    public IReadOnlyCollection<ExamAnswer> Answers => _answers.AsReadOnly();

    public IReadOnlyCollection<IntegrityFlag> IntegrityFlags => _integrityFlags.AsReadOnly();

    public ExamAttemptStatus Status { get; private set; }

    public DateTimeOffset StartedAt { get; private set; }

    /// <summary>requirement-spec.md §5: "the server clock is authoritative, never the client's" - every expiry check compares against this, computed once at <see cref="Start"/> time.</summary>
    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? SubmittedAt { get; private set; }

    public string? SubmissionSource { get; private set; }

    public EvaluationStatus EvaluationStatus { get; private set; }

    public decimal? ObjectiveScore { get; private set; }

    public decimal? SubjectiveScore { get; private set; }

    public decimal? TotalScore => ObjectiveScore is { } o && SubjectiveScore is { } s ? o + s : null;

    public static Result<ExamAttempt> Start(Guid applicantId, Guid admissionTestId, Guid testSlotId, string rollNumber, long seedValue, IReadOnlyList<QuestionId> selectedQuestionIds, int timeLimitMinutes, DateTimeOffset now)
    {
        if (applicantId == Guid.Empty || admissionTestId == Guid.Empty || testSlotId == Guid.Empty)
        {
            return Error.Validation("exam_attempt.identifiers_required", "An ExamAttempt requires applicantId, admissionTestId, and testSlotId.");
        }

        if (string.IsNullOrWhiteSpace(rollNumber))
        {
            return Error.Validation("exam_attempt.roll_number_required", "An ExamAttempt requires a rollNumber.");
        }

        if (selectedQuestionIds is not { Count: > 0 })
        {
            return Error.Validation("exam_attempt.questions_required", "An ExamAttempt requires at least one selected question.");
        }

        return new ExamAttempt(ExamAttemptId.New(), applicantId, admissionTestId, testSlotId, rollNumber.Trim(), seedValue, selectedQuestionIds, now, now.AddMinutes(timeLimitMinutes));
    }

    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;

    public void RecordProctoringSession(string? providerSessionId)
    {
        ProctoringSessionId = providerSessionId;
    }

    /// <summary>ADM-12: called only while <see cref="ExamAttemptStatus.InProgress"/> AND before <see cref="ExpiresAt"/> - a stale in-flight auto-save (class remarks) is a silent no-op rather than an error, since the client cannot distinguish "too late" from "already applied" without extra round trips it has no use for.</summary>
    public void SaveAnswer(QuestionId questionId, int? selectedOptionIndex, string? subjectiveText, DateTimeOffset now)
    {
        if (Status != ExamAttemptStatus.InProgress || now >= ExpiresAt)
        {
            return;
        }

        if (!_selectedQuestionIds.Contains(questionId))
        {
            return;
        }

        var existing = _answers.FirstOrDefault(a => a.QuestionId == questionId);
        if (existing is null)
        {
            _answers.Add(new ExamAnswer(questionId, selectedOptionIndex, subjectiveText, now));
        }
        else
        {
            existing.Update(selectedOptionIndex, subjectiveText, now);
        }
    }

    /// <summary>Called only by the repository's own already-won conditional write - see class remarks. <paramref name="source"/> is <c>"manual"</c> or <c>"timeout-sweep"</c> (design-decisions.md).</summary>
    public void Submit(string source, DateTimeOffset now)
    {
        Status = ExamAttemptStatus.Submitted;
        SubmittedAt = now;
        SubmissionSource = source;
        Raise(new ExamAttemptSubmitted(Id.Value, ApplicantId, AdmissionTestId, source, now));
    }

    /// <summary>requirement-spec.md §2: "Evaluation ... runs after the attempt locks, never before." Objective auto-scoring is computed by the calling service (which alone has access to the sibling AdmissionTest aggregate's own correct-answer data) and recorded here.</summary>
    public Result RecordEvaluation(decimal objectiveScore, decimal? subjectiveScore, DateTimeOffset now)
    {
        if (Status != ExamAttemptStatus.Submitted)
        {
            return Result.Failure(Error.Conflict("exam_attempt.not_submitted", $"ExamAttempt '{Id}' cannot be evaluated - it is not yet Submitted."));
        }

        ObjectiveScore = objectiveScore;
        SubjectiveScore = subjectiveScore;
        EvaluationStatus = subjectiveScore is not null ? EvaluationStatus.Evaluated : EvaluationStatus.Pending;
        _ = now;
        return Result.Success();
    }

    /// <summary>Records the manually-evaluated subjective component once available, completing evaluation.</summary>
    public Result RecordSubjectiveScore(decimal subjectiveScore)
    {
        if (ObjectiveScore is null)
        {
            return Result.Failure(Error.Conflict("exam_attempt.objective_score_missing", $"ExamAttempt '{Id}' has no objective score recorded yet."));
        }

        SubjectiveScore = subjectiveScore;
        EvaluationStatus = EvaluationStatus.Evaluated;
        return Result.Success();
    }

    /// <summary>ADR-0018: never blocks/mutates the attempt itself - purely additive, reviewable evidence.</summary>
    public IntegrityFlag RaiseIntegrityFlag(string anomalyType, string details, decimal confidenceScore, DateTimeOffset now)
    {
        var flag = new IntegrityFlag(Guid.NewGuid(), anomalyType, details, confidenceScore, now);
        _integrityFlags.Add(flag);
        return flag;
    }

    public Result ReviewIntegrityFlag(Guid flagId, IntegrityFlagOutcome outcome, Guid reviewedByUserId, string? notes, DateTimeOffset now)
    {
        var flag = _integrityFlags.FirstOrDefault(f => f.Id == flagId);
        if (flag is null)
        {
            return Result.Failure(Error.NotFound("exam_attempt.flag_not_found", $"No IntegrityFlag '{flagId}' exists on ExamAttempt '{Id}'."));
        }

        flag.Review(outcome, reviewedByUserId, notes, now);
        return Result.Success();
    }
}
