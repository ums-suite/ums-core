using UMS.Modules.Research.Domain.Common;

namespace UMS.Modules.Research.Domain.Events;

public sealed record GrantWithdrawn(Guid GrantId, DateTimeOffset OccurredAt) : IDomainEvent;
