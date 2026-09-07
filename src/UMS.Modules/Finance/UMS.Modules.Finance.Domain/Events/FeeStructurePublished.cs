using UMS.Modules.Finance.Domain.Common;

namespace UMS.Modules.Finance.Domain.Events;

public sealed record FeeStructurePublished(Guid FeeStructureId, string FeeType, int VersionNumber, decimal Amount, DateTimeOffset OccurredAt) : IDomainEvent;
