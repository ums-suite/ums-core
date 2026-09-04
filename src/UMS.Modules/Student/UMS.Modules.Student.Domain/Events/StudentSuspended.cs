using UMS.Modules.Student.Domain.Common;

namespace UMS.Modules.Student.Domain.Events;

public sealed record StudentSuspended(Guid StudentId, string? Reason, DateTimeOffset OccurredAt) : IDomainEvent;
