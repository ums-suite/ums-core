using UMS.Modules.Admission.Domain.Common;

namespace UMS.Modules.Admission.Domain.Events;

/// <summary>requirement-spec.md §3: "Auto- or manual submit" - consumed by Audit (single-submission proof).</summary>
public sealed record ExamAttemptSubmitted(Guid ExamAttemptId, Guid ApplicantId, Guid AdmissionTestId, string Source, DateTimeOffset OccurredAt) : IDomainEvent;
