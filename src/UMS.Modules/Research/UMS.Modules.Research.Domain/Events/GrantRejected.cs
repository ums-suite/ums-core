using UMS.Modules.Research.Domain.Common;

namespace UMS.Modules.Research.Domain.Events;

public sealed record GrantRejected(Guid GrantId, DateTimeOffset OccurredAt) : IDomainEvent;
