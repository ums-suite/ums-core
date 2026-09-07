using UMS.Modules.Student.Domain.Common;

namespace UMS.Modules.Student.Domain.Events;

/// <summary>requirement-spec.md student §3: "Async job finishes" - STU-15, consumed by Notifications (to the initiating admin) and Audit.</summary>
public sealed record StudentBulkImportCompleted(Guid JobId, Guid RequestedByUserId, int SucceededCount, int FailedCount, DateTimeOffset OccurredAt) : IDomainEvent;
