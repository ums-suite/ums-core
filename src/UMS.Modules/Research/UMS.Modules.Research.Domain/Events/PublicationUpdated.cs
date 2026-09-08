using UMS.Modules.Research.Domain.Common;

namespace UMS.Modules.Research.Domain.Events;

public sealed record PublicationUpdated(Guid PublicationId, DateTimeOffset OccurredAt) : IDomainEvent;
