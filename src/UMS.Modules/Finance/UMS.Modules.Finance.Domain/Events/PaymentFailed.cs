using UMS.Modules.Finance.Domain.Common;

namespace UMS.Modules.Finance.Domain.Events;

public sealed record PaymentFailed(Guid PaymentId, Guid InvoiceId, Guid OwnerId, string Reason, DateTimeOffset OccurredAt) : IDomainEvent;
