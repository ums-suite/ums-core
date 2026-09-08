using UMS.Modules.Research.Domain.Common;

namespace UMS.Modules.Research.Domain.Events;

public sealed record GrantActivated(Guid GrantId, DateTimeOffset OccurredAt) : IDomainEvent;
