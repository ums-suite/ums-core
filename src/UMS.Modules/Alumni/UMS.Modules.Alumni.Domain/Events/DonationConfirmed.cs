using UMS.Modules.Alumni.Domain.Common;

namespace UMS.Modules.Alumni.Domain.Events;

/// <summary>requirement-spec.md §3: raised when Finance's PaymentCompleted signal confirms a donation-tagged invoice. Consumers: Notifications (receipt, ALM-15), Reporting.</summary>
public sealed record DonationConfirmed(Guid DonationId, Guid AlumnusId, DateTimeOffset OccurredAt) : IDomainEvent;
