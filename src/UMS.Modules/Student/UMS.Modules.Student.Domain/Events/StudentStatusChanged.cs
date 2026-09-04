using UMS.Modules.Student.Domain.Common;
using UMS.Modules.Student.Domain.Students;

namespace UMS.Modules.Student.Domain.Events;

/// <summary>requirement-spec.md student §3: "Any lifecycle transition" - mandatory-Audit-consumer event (ums-requirements.md §4.1).</summary>
public sealed record StudentStatusChanged(Guid StudentId, StudentStatus? PreviousStatus, StudentStatus NewStatus, DateTimeOffset OccurredAt) : IDomainEvent;
