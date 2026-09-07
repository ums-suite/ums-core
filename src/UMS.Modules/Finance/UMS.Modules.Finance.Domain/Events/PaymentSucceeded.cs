using UMS.Modules.Finance.Domain.Common;

namespace UMS.Modules.Finance.Domain.Events;

/// <summary>Consumed by Documents (sync receipt generation), Notifications, the calling module, Reporting (requirement-spec.md finance §6).</summary>
public sealed record PaymentSucceeded(Guid PaymentId, Guid InvoiceId, Guid OwnerId, string SourceModule, string SourceReferenceId, decimal Amount, DateTimeOffset OccurredAt) : IDomainEvent;
