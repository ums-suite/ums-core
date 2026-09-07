namespace UMS.Modules.Finance.Domain.Common;

/// <summary>
/// A behavior-bearing aggregate root (ums-conventions.md, Domain Modeling). Mirrors
/// <c>UMS.Modules.Learning.Domain.Common.AggregateRoot{TId}</c> exactly.
///
/// <para>
/// <see cref="Version"/> (Postgres <c>xmin</c>-backed optimistic concurrency) backs every Finance
/// aggregate's ordinary field updates. It is deliberately NOT the mechanism that protects the two
/// invariants design-decisions.md calls out as needing something stronger: Invoice-level payment
/// initiation is guarded by a pessimistic <c>SELECT ... FOR UPDATE</c> row lock (design-decisions.md
/// "Invoice-Level Concurrency Control for Payment Initiation"), and a webhook/polling-job state
/// transition is guarded by the monotonic ordering function on <see cref="Payments.PaymentTransaction"/>
/// itself, not by <c>xmin</c> (design-decisions.md "Webhook Signature Verification and
/// State-Transition Ordering") - <c>xmin</c> alone would only detect a lost update after the fact,
/// not prevent the double-payment race those two decisions are about.
/// </para>
/// </summary>
public abstract class AggregateRoot<TId> : IHasDomainEvents
    where TId : notnull
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public TId Id { get; protected set; } = default!;

    public uint Version { get; protected set; }

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public void ClearDomainEvents() => _domainEvents.Clear();

    protected void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
}
