using UMS.Modules.Alumni.Domain.Common;
using UMS.Modules.Alumni.Domain.Events;

namespace UMS.Modules.Alumni.Domain.Donations;

/// <summary>
/// ALM-8/ALM-9/ALM-10: the Donation aggregate root (requirement-spec.md §2.4, §3, §4).
///
/// <para>
/// design-decisions.md "Donation Confirmation Consistency Model": stays <see cref="DonationStatus.Pending"/>
/// until Finance's <c>PaymentCompleted</c>/<c>PaymentFailed</c> webhook-verified event arrives
/// (consumed via the same outbox-polling mechanism as StudentGraduated) - never confirmed on donor
/// redirect, never a synchronous wait.
/// </para>
///
/// <para>
/// A recurring Donation is modeled as a SERIES of one-off Donation rows sharing
/// <see cref="SeriesRootDonationId"/> (null on the series root itself, the root's own id on every
/// generated cycle) - <see cref="RecurrenceStatus"/> is authoritative ONLY on the root row; the
/// scheduler (ALM-10) reads the root before creating each new cycle. design-decisions.md
/// "Recurring-Donation Retry/Dunning Policy": the FIRST <c>PaymentFailed</c> for any cycle pauses the
/// root's own schedule immediately - no automated retry of that cycle is ever attempted.
/// </para>
/// </summary>
public sealed class Donation : AggregateRoot<DonationId>
{
    private Donation()
    {
    }

    private Donation(DonationId id, Guid alumnusId, Guid campaignId, decimal amount, string currency, bool isAnonymous, RecurrenceInterval recurrenceInterval, DonationId? seriesRootDonationId, DateTimeOffset now)
    {
        Id = id;
        AlumnusId = alumnusId;
        CampaignId = campaignId;
        Amount = amount;
        Currency = currency;
        IsAnonymous = isAnonymous;
        RecurrenceInterval = recurrenceInterval;
        RecurrenceStatus = recurrenceInterval == RecurrenceInterval.None ? null : Domain.Donations.RecurrenceStatus.Active;
        SeriesRootDonationId = seriesRootDonationId;
        Status = DonationStatus.Pending;
        CreatedAt = now;
    }

    public Guid AlumnusId { get; private set; }

    public Guid CampaignId { get; private set; }

    public decimal Amount { get; private set; }

    public string Currency { get; private set; } = "BDT";

    /// <summary>requirement-spec.md §4/§2.4: display-only - the real AlumnusId link above is always retained for reconciliation/audit.</summary>
    public bool IsAnonymous { get; private set; }

    public RecurrenceInterval RecurrenceInterval { get; private set; }

    public RecurrenceStatus? RecurrenceStatus { get; private set; }

    public DonationId? SeriesRootDonationId { get; private set; }

    public bool IsRecurring => RecurrenceInterval != RecurrenceInterval.None;

    public bool IsSeriesRoot => IsRecurring && SeriesRootDonationId is null;

    public DonationStatus Status { get; private set; }

    public Guid? InvoiceId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? ConfirmedAt { get; private set; }

    public DateTimeOffset? NextChargeAt { get; private set; }

    public static Donation Initiate(Guid alumnusId, Guid campaignId, decimal amount, string currency, bool isAnonymous, RecurrenceInterval recurrenceInterval, DateTimeOffset now)
    {
        if (amount <= 0)
        {
            throw new ArgumentException("A Donation's amount must be positive.", nameof(amount));
        }

        return new Donation(DonationId.New(), alumnusId, campaignId, amount, currency, isAnonymous, recurrenceInterval, seriesRootDonationId: null, now);
    }

    /// <summary>ALM-10: the scheduler's own next-cycle factory - copies the root's donor/campaign/amount/anonymity, always one-off in shape (its OWN recurrence is tracked back on the root, not re-declared per cycle).</summary>
    public static Donation CreateNextCycle(Donation root, DateTimeOffset now)
    {
        if (!root.IsSeriesRoot)
        {
            throw new InvalidOperationException("Only a recurring series' root Donation may generate a next cycle.");
        }

        return new Donation(DonationId.New(), root.AlumnusId, root.CampaignId, root.Amount, root.Currency, root.IsAnonymous, root.RecurrenceInterval, root.Id, now);
    }

    public void RecordInvoice(Guid invoiceId) => InvoiceId = invoiceId;

    /// <summary>design-decisions.md "Donation Confirmation Consistency Model" - the ONLY path to Confirmed.</summary>
    public void Confirm(DateTimeOffset now)
    {
        if (Status != DonationStatus.Pending)
        {
            return;
        }

        Status = DonationStatus.Confirmed;
        ConfirmedAt = now;
        Raise(new DonationConfirmed(Id.Value, AlumnusId, now));

        if (IsSeriesRoot && RecurrenceStatus == Domain.Donations.RecurrenceStatus.Active)
        {
            NextChargeAt = ComputeNextChargeAt(now);
        }
    }

    /// <summary>design-decisions.md "Recurring-Donation Retry/Dunning Policy": pause on first failure - no retry of this cycle, ever.</summary>
    public void Fail(DateTimeOffset now)
    {
        if (Status != DonationStatus.Pending)
        {
            return;
        }

        Status = DonationStatus.Failed;

        if (IsSeriesRoot)
        {
            RecurrenceStatus = Domain.Donations.RecurrenceStatus.Paused;
            NextChargeAt = null;
        }
    }

    /// <summary>ALM-10: <c>POST /donations/{id}/cancel-recurring</c> - donor-initiated, any time.</summary>
    public void CancelRecurring()
    {
        if (!IsSeriesRoot)
        {
            throw new InvalidOperationException("Only a recurring series' root Donation can have its recurrence cancelled.");
        }

        RecurrenceStatus = Domain.Donations.RecurrenceStatus.Cancelled;
        NextChargeAt = null;
    }

    /// <summary>Explicit donor action resuming a Paused schedule (requirement-spec.md §8: "donor must explicitly resume/update payment method").</summary>
    public void ResumeRecurring(DateTimeOffset now)
    {
        if (!IsSeriesRoot || RecurrenceStatus != Domain.Donations.RecurrenceStatus.Paused)
        {
            throw new InvalidOperationException("Only a Paused recurring series' root Donation can be resumed.");
        }

        RecurrenceStatus = Domain.Donations.RecurrenceStatus.Active;
        NextChargeAt = ComputeNextChargeAt(now);
    }

    private DateTimeOffset ComputeNextChargeAt(DateTimeOffset from) => RecurrenceInterval switch
    {
        RecurrenceInterval.Monthly => from.AddMonths(1),
        RecurrenceInterval.Quarterly => from.AddMonths(3),
        RecurrenceInterval.Yearly => from.AddYears(1),
        _ => from,
    };
}
