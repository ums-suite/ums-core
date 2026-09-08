using UMS.Modules.Finance.Domain.Common;

namespace UMS.Modules.Finance.Domain.Events;

/// <summary>FIN-11: raised the instant a Refund request passes Payment's own validation (status + amount-ceiling), before the gateway/manual-settlement outcome is known - kept distinct from <see cref="RefundCompleted"/> for parity with requirement-spec.md finance §3's own domain-event list, and so a future asynchronous gateway-refund flow has an existing seam to attach to without a breaking change.</summary>
public sealed record RefundRequested(Guid RefundId, Guid PaymentId, Guid OwnerId, decimal Amount, DateTimeOffset OccurredAt) : IDomainEvent;
