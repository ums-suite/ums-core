using UMS.Modules.Student.Domain.Common;
using UMS.Modules.Student.Domain.Guardians;

namespace UMS.Modules.Student.Domain.Events;

public sealed record GuardianAccessRevoked(Guid StudentId, Guid GuardianId, GuardianAccessCategory Category, DateTimeOffset OccurredAt) : IDomainEvent;
