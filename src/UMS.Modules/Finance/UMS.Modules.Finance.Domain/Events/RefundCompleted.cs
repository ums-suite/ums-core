using UMS.Modules.Finance.Domain.Common;

namespace UMS.Modules.Finance.Domain.Events;

/// <summary>Consumed by Notifications, the calling module (Admission/Student/Hostel/Alumni), and Reporting (requirement-spec.md finance §6).</summary>
public sealed record RefundCompleted(Guid RefundId, Guid PaymentId, Guid InvoiceId, Guid OwnerId, decimal Amount, DateTimeOffset OccurredAt) : IDomainEvent;
