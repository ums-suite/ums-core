using UMS.Modules.Student.Domain.Common;

namespace UMS.Modules.Student.Domain.Events;

public sealed record StudentTransferred(Guid StudentId, string? Reason, DateTimeOffset OccurredAt) : IDomainEvent;
