using UMS.Modules.Academic.Domain.Common;

namespace UMS.Modules.Academic.Domain.Events;

/// <summary>ACD-13: the correction workflow completed - before/after value mandatory (requirement-spec.md §3, ums-requirements.md §4.1).</summary>
public sealed record GradeCorrected(Guid GradeId, Guid EnrollmentId, Guid ResultPublicationId, decimal PreviousScore, decimal NewScore, string Reason, Guid CorrectedByUserId, DateTimeOffset OccurredAt) : IDomainEvent;
