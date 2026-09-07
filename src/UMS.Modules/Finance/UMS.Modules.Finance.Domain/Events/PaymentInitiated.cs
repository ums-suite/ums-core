using UMS.Modules.Finance.Domain.Common;

namespace UMS.Modules.Finance.Domain.Events;

public sealed record PaymentInitiated(Guid PaymentId, Guid InvoiceId, decimal Amount, DateTimeOffset OccurredAt) : IDomainEvent;
