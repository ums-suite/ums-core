using UMS.Modules.Student.Domain.Common;

namespace UMS.Modules.Student.Domain.Events;

public sealed record GuardianLinked(Guid StudentId, Guid GuardianId, DateTimeOffset OccurredAt) : IDomainEvent;
