using UMS.Modules.Research.Domain.Common;

namespace UMS.Modules.Research.Domain.Events;

public sealed record InstitutionalRepositoryEntryDeposited(Guid RepositoryEntryId, DateTimeOffset OccurredAt) : IDomainEvent;
