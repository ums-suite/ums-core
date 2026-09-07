using UMS.Modules.Learning.Domain.Common;

namespace UMS.Modules.Learning.Domain.Events;

/// <summary>LRN-8: the provider returned a result (requirement-spec.md learning §3). Consumers: Notifications (Instructor). Advisory evidence only - never a verdict this platform acts on itself (ADR-0018).</summary>
public sealed record PlagiarismCheckCompleted(
    Guid PlagiarismCheckId,
    Guid SubmissionId,
    Guid AssignmentId,
    decimal SimilarityPercentage,
    string ProviderName,
    Guid InstructorUserId,
    DateTimeOffset OccurredAt) : IDomainEvent;
