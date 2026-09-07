using UMS.Modules.Admission.Application.Abstractions;
using UMS.Modules.Admission.Application.Common;
using UMS.Modules.Admission.Domain.Applications;
using UMS.Modules.Admission.Domain.ExamAttempts;
using UMS.Modules.Admission.Domain.Tests;
using UMS.Shared.Audit;
using UMS.Shared.ErrorHandling.Results;
using UMS.Shared.Integrations;
using ApplicationId = UMS.Modules.Admission.Domain.Applications.ApplicationId;

namespace UMS.Modules.Admission.Application.ExamAttempts;

/// <summary>ADM-11..14: the Admission Test pipeline (requirement-spec.md §2 Admission Test Lifecycle).</summary>
public sealed class ExamAttemptService(
    IExamAttemptRepository attempts,
    IAdmissionTestRepository tests,
    IApplicationRepository applications,
    IProctoringProvider proctoring,
    IUnitOfWork unitOfWork,
    IDomainEventRecorder domainEvents,
    IAuditRecorder auditRecorder,
    IClock clock)
{
    /// <summary>ADM-11: server-side seeded random question selection (§9 decision 4) - one attempt per (Applicant, AdmissionTest), enforced both here and by a DB-level unique index.</summary>
    public async Task<Result<ExamAttemptDto>> StartAsync(Guid applicationId, string correlationId, CancellationToken cancellationToken = default)
    {
        var application = await applications.GetByIdAsync(new ApplicationId(applicationId), cancellationToken).ConfigureAwait(false);
        if (application is null)
        {
            return Error.NotFound("exam_attempt.application_not_found", $"No Application exists with id '{applicationId}'.");
        }

        if (application.Status != ApplicationStatus.Locked || application.AssignedTestSlotId is null)
        {
            return Error.Conflict("exam_attempt.not_admit_eligible", "The Application must be Locked and have an assigned test slot before an ExamAttempt may start.");
        }

        var test = await tests.GetByCampaignIdAsync(application.CampaignId, cancellationToken).ConfigureAwait(false);
        if (test is null)
        {
            return Error.NotFound("exam_attempt.test_not_found", $"No AdmissionTest exists for Campaign '{application.CampaignId}'.");
        }

        var existing = await attempts.GetByApplicantAndTestAsync(application.ApplicantId, test.Id.Value, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return Error.Conflict("exam_attempt.already_started", $"Applicant '{application.ApplicantId}' already has an ExamAttempt for AdmissionTest '{test.Id}'.");
        }

        var seed = Random.Shared.NextInt64();
        var selection = test.SelectQuestions(seed);
        if (selection.IsFailure)
        {
            return selection.Error!;
        }

        var created = ExamAttempt.Start(application.ApplicantId, test.Id.Value, application.AssignedTestSlotId.Value, application.RollNumber ?? string.Empty, seed, selection.Value, test.DurationMinutes, clock.UtcNow);
        if (created.IsFailure)
        {
            return created.Error!;
        }

        var session = await proctoring.StartSessionAsync(new StartProctoringSessionCommand(created.Value.Id.Value, application.ApplicantId, ReferencePhotoUrl: null), cancellationToken).ConfigureAwait(false);
        created.Value.RecordProctoringSession(session.IsSuccess ? session.Value.ProviderSessionId : null);

        attempts.Add(created.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDto(created.Value);
    }

    public async Task<Result> SaveAnswerAsync(Guid examAttemptId, SaveAnswerRequest request, CancellationToken cancellationToken = default)
    {
        var attempt = await attempts.GetByIdAsync(new ExamAttemptId(examAttemptId), cancellationToken).ConfigureAwait(false);
        if (attempt is null)
        {
            return Result.Failure(Error.NotFound("exam_attempt.not_found", $"No ExamAttempt exists with id '{examAttemptId}'."));
        }

        attempt.SaveAnswer(new QuestionId(request.QuestionId), request.SelectedOptionIndex, request.SubjectiveText, clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    /// <summary>ADM-13: manual submit - converges on the identical conditional write the timeout sweep uses (design-decisions.md).</summary>
    public Task<Result<ExamAttemptDto>> SubmitAsync(Guid examAttemptId, string correlationId, CancellationToken cancellationToken = default) =>
        TryLockAndEvaluateAsync(examAttemptId, "manual", correlationId, cancellationToken);

    public async Task<Result> RecordSubjectiveScoreAsync(Guid examAttemptId, decimal subjectiveScore, CancellationToken cancellationToken = default)
    {
        var attempt = await attempts.GetByIdAsync(new ExamAttemptId(examAttemptId), cancellationToken).ConfigureAwait(false);
        if (attempt is null)
        {
            return Result.Failure(Error.NotFound("exam_attempt.not_found", $"No ExamAttempt exists with id '{examAttemptId}'."));
        }

        var recorded = attempt.RecordSubjectiveScore(subjectiveScore);
        if (recorded.IsFailure)
        {
            return recorded;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    /// <summary>ADR-0018: a continuous-monitoring anomaly report - never auto-disqualifying.</summary>
    public async Task<Result> RaiseIntegrityFlagAsync(Guid examAttemptId, string anomalyType, string details, decimal confidenceScore, CancellationToken cancellationToken = default)
    {
        var attempt = await attempts.GetByIdAsync(new ExamAttemptId(examAttemptId), cancellationToken).ConfigureAwait(false);
        if (attempt is null)
        {
            return Result.Failure(Error.NotFound("exam_attempt.not_found", $"No ExamAttempt exists with id '{examAttemptId}'."));
        }

        attempt.RaiseIntegrityFlag(anomalyType, details, confidenceScore, clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    /// <summary>ADR-0018: "a human always makes the final call" - the Admission Officer's own review.</summary>
    public async Task<Result> ReviewIntegrityFlagAsync(Guid examAttemptId, Guid flagId, IntegrityFlagOutcome outcome, Guid reviewerUserId, string? notes, CancellationToken cancellationToken = default)
    {
        var attempt = await attempts.GetByIdAsync(new ExamAttemptId(examAttemptId), cancellationToken).ConfigureAwait(false);
        if (attempt is null)
        {
            return Result.Failure(Error.NotFound("exam_attempt.not_found", $"No ExamAttempt exists with id '{examAttemptId}'."));
        }

        var reviewed = attempt.ReviewIntegrityFlag(flagId, outcome, reviewerUserId, notes, clock.UtcNow);
        if (reviewed.IsFailure)
        {
            return reviewed;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    public async Task<Result<ExamAttemptDto>> GetByIdAsync(Guid examAttemptId, CancellationToken cancellationToken = default)
    {
        var attempt = await attempts.GetByIdAsync(new ExamAttemptId(examAttemptId), cancellationToken).ConfigureAwait(false);
        return attempt is null
            ? Error.NotFound("exam_attempt.not_found", $"No ExamAttempt exists with id '{examAttemptId}'.")
            : ToDto(attempt);
    }

    internal static ExamAttemptDto ToDto(ExamAttempt attempt) => new(
        attempt.Id.Value,
        attempt.ApplicantId,
        attempt.AdmissionTestId,
        attempt.RollNumber,
        attempt.Status.ToString(),
        attempt.StartedAt,
        attempt.ExpiresAt,
        attempt.SelectedQuestionIds.Select(q => q.Value).ToList(),
        attempt.Answers.Select(a => new ExamAnswerDto(a.QuestionId.Value, a.SelectedOptionIndex, a.SubjectiveText)).ToList(),
        attempt.EvaluationStatus.ToString(),
        attempt.ObjectiveScore,
        attempt.SubjectiveScore,
        attempt.IntegrityFlags.Select(f => new IntegrityFlagDto(f.Id, f.AnomalyType, f.Details, f.ConfidenceScore, f.Outcome.ToString())).ToList());

    internal async Task<Result<ExamAttemptDto>> TryLockAndEvaluateAsync(Guid examAttemptId, string source, string correlationId, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var locked = await attempts.TryLockAsync(new ExamAttemptId(examAttemptId), source, now, cancellationToken).ConfigureAwait(false);

        var attempt = await attempts.GetByIdAsync(new ExamAttemptId(examAttemptId), cancellationToken).ConfigureAwait(false);
        if (attempt is null)
        {
            return Error.NotFound("exam_attempt.not_found", $"No ExamAttempt exists with id '{examAttemptId}'.");
        }

        if (!locked)
        {
            // A concurrent writer already won (double-submit/timeout-sweep race) - replay the
            // already-locked state, never an error (design-decisions.md).
            return ToDto(attempt);
        }

        domainEvents.Enqueue(new Domain.Events.ExamAttemptSubmitted(attempt.Id.Value, attempt.ApplicantId, attempt.AdmissionTestId, source, now));

        var auditRequest = AuditContext.ForSystemJob($"exam-attempt-{source}", correlationId, "ExamAttempt", attempt.Id.Value.ToString(), AuditActions.Update, "{\"status\":\"InProgress\"}", $"{{\"status\":\"Submitted\",\"source\":\"{source}\"}}");
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var committed = await TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, auditRequest, cancellationToken).ConfigureAwait(false);
        if (committed.IsFailure)
        {
            return committed.Error!;
        }

        await EvaluateObjectiveAsync(attempt, cancellationToken).ConfigureAwait(false);

        var reloaded = await attempts.GetByIdAsync(attempt.Id, cancellationToken).ConfigureAwait(false);
        return reloaded is null ? Error.Failure("exam_attempt.not_found", "ExamAttempt vanished after submit.") : ToDto(reloaded);
    }

    /// <summary>requirement-spec.md §2: "Evaluation ... auto-scored for objective questions; manual for any subjective component" - runs only after the attempt locks.</summary>
    private async Task EvaluateObjectiveAsync(ExamAttempt attempt, CancellationToken cancellationToken)
    {
        var test = await tests.GetByIdAsync(new AdmissionTestId(attempt.AdmissionTestId), cancellationToken).ConfigureAwait(false);
        if (test is null)
        {
            return;
        }

        var hasSubjective = false;
        decimal objectiveScore = 0;
        foreach (var questionId in attempt.SelectedQuestionIds)
        {
            var question = test.Questions.FirstOrDefault(q => q.Id == questionId);
            if (question is null || question.IsSubjective)
            {
                hasSubjective = hasSubjective || question is { IsSubjective: true };
                continue;
            }

            var answer = attempt.Answers.FirstOrDefault(a => a.QuestionId == questionId);
            if (answer?.SelectedOptionIndex == question.CorrectOptionIndex)
            {
                objectiveScore += question.MaxScore;
            }
        }

        attempt.RecordEvaluation(objectiveScore, hasSubjective ? null : 0, clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
