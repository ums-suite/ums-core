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

    private Donation(DonationId id, Guid alumnusId, Guid ownerUserId, Guid campaignId, decimal amount, string currency, bool isAnonymous, RecurrenceInterval recurrenceInterval, DonationId? seriesRootDonationId, DateTimeOffset now)
    {
        Id = id;
        AlumnusId = alumnusId;
        OwnerUserId = ownerUserId;
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

    /// <summary>
    /// The Identity <c>User</c> id to raise each cycle's Finance Invoice against (captured from the
    /// donor's own JWT at the ROOT donation's creation time and copied forward onto every generated
    /// cycle via <see cref="CreateNextCycle"/>) - ALM-10's scheduler runs with no HTTP/JWT context of
    /// its own, so this must be persisted rather than re-resolved per cycle.
    /// </summary>
    public Guid OwnerUserId { get; private set; }

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

    public static Donation Initiate(Guid alumnusId, Guid ownerUserId, Guid campaignId, decimal amount, string currency, bool isAnonymous, RecurrenceInterval recurrenceInterval, DateTimeOffset now)
    {
        if (amount <= 0)
        {
            throw new ArgumentException("A Donation's amount must be positive.", nameof(amount));
        }

        var donation = new Donation(DonationId.New(), alumnusId, ownerUserId, campaignId, amount, currency, isAnonymous, recurrenceInterval, seriesRootDonationId: null, now);

        // The first future charge is scheduled at creation time, not on confirmation - Confirm/Fail
        // decouple "did THIS cycle succeed" from "when is the next cycle due" (see AdvanceScheduleAfterTriggering/PauseRecurrence's own remarks). A first-cycle failure still clears this via Fail().
        if (donation.IsRecurring)
        {
            donation.NextChargeAt = donation.ComputeNextChargeAt(now);
        }

        return donation;
    }

    /// <summary>ALM-10: the scheduler's own next-cycle factory - copies the root's donor/campaign/amount/anonymity, always one-off in shape (its OWN recurrence is tracked back on the root, not re-declared per cycle).</summary>
    public static Donation CreateNextCycle(Donation root, DateTimeOffset now)
    {
        if (!root.IsSeriesRoot)
        {
            throw new InvalidOperationException("Only a recurring series' root Donation may generate a next cycle.");
        }

        return new Donation(DonationId.New(), root.AlumnusId, root.OwnerUserId, root.CampaignId, root.Amount, root.Currency, root.IsAnonymous, root.RecurrenceInterval, root.Id, now);
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
            PauseRecurrence();
        }
    }

    /// <summary>
    /// Pauses the schedule following a failure ANYWHERE in the series - called on the ROOT entity
    /// even when the failing cycle is a later, non-root Donation row (<see cref="Fail"/> only ever
    /// mutates the row it is called on, so a non-root cycle's own failure cannot pause the schedule
    /// by itself; the calling application service - <c>DonationConfirmationService</c> - loads the
    /// root separately and calls this).
    /// </summary>
    public void PauseRecurrence()
    {
        if (!IsSeriesRoot)
        {
            throw new InvalidOperationException("Only a recurring series' root Donation's recurrence can be paused.");
        }

        RecurrenceStatus = Domain.Donations.RecurrenceStatus.Paused;
        NextChargeAt = null;
    }

    /// <summary>ALM-10 scheduler-only: advances the root's own next-charge date immediately after successfully raising a new cycle's invoice - decouples "when to try next" from that cycle's own eventual Confirm/Fail outcome (a genuine failure separately pauses the schedule via <see cref="PauseRecurrence"/> once Finance's signal arrives).</summary>
    public void AdvanceScheduleAfterTriggering(DateTimeOffset now)
    {
        if (!IsSeriesRoot || RecurrenceStatus != Domain.Donations.RecurrenceStatus.Active)
        {
            throw new InvalidOperationException("Only an Active recurring series' root Donation can have its schedule advanced.");
        }

        NextChargeAt = ComputeNextChargeAt(now);
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
