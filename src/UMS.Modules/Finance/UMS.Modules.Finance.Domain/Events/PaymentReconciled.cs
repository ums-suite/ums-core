using UMS.Modules.Finance.Domain.Common;

namespace UMS.Modules.Finance.Domain.Events;

/// <summary>FIN-14: raised on a Successful -&gt; Reconciled match against the gateway's settlement report. Consumed by Reporting (requirement-spec.md finance §6).</summary>
public sealed record PaymentReconciled(Guid PaymentId, Guid InvoiceId, DateTimeOffset OccurredAt) : IDomainEvent;
