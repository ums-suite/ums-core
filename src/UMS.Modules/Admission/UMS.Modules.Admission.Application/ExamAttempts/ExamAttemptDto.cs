namespace UMS.Modules.Admission.Application.ExamAttempts;

public sealed record ExamAttemptDto(
    Guid Id,
    Guid ApplicantId,
    Guid AdmissionTestId,
    string RollNumber,
    string Status,
    DateTimeOffset StartedAt,
    DateTimeOffset ExpiresAt,
    IReadOnlyCollection<Guid> SelectedQuestionIds,
    IReadOnlyCollection<ExamAnswerDto> Answers,
    string EvaluationStatus,
    decimal? ObjectiveScore,
    decimal? SubjectiveScore,
    IReadOnlyCollection<IntegrityFlagDto> IntegrityFlags);

public sealed record ExamAnswerDto(Guid QuestionId, int? SelectedOptionIndex, string? SubjectiveText);

public sealed record IntegrityFlagDto(Guid Id, string AnomalyType, string Details, decimal ConfidenceScore, string Outcome);

public sealed record SaveAnswerRequest(Guid QuestionId, int? SelectedOptionIndex, string? SubjectiveText);
