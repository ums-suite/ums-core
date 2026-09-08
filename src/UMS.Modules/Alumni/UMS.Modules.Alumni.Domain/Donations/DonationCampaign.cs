using UMS.Modules.Alumni.Domain.Common;

namespace UMS.Modules.Alumni.Domain.Donations;

/// <summary>ALM-8: "a lightweight Alumni-owned reference (name, goal, active window) - not a full Finance FeeStructure" (requirement-spec.md §2.4/§3).</summary>
public sealed class DonationCampaign : AggregateRoot<DonationCampaignId>
{
    private DonationCampaign()
    {
    }

    private DonationCampaign(DonationCampaignId id, string name, string? description, decimal goalAmount, string currency, DateTimeOffset startsAt, DateTimeOffset endsAt, DateTimeOffset now)
    {
        Id = id;
        Name = name;
        Description = description;
        GoalAmount = goalAmount;
        Currency = currency;
        StartsAt = startsAt;
        EndsAt = endsAt;
        CreatedAt = now;
    }

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public decimal GoalAmount { get; private set; }

    public string Currency { get; private set; } = "BDT";

    public DateTimeOffset StartsAt { get; private set; }

    public DateTimeOffset EndsAt { get; private set; }

    /// <summary>An Admin's early-close overrides <see cref="EndsAt"/> as the effective close moment (requirement-spec.md §2.4's "active window").</summary>
    public bool ClosedEarly { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static DonationCampaign Create(string name, string? description, decimal goalAmount, string currency, DateTimeOffset startsAt, DateTimeOffset endsAt, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A DonationCampaign's name is required.", nameof(name));
        }

        if (endsAt <= startsAt)
        {
            throw new ArgumentException("A DonationCampaign's endsAt must be after startsAt.", nameof(endsAt));
        }

        return new DonationCampaign(DonationCampaignId.New(), name.Trim(), description?.Trim(), goalAmount, currency, startsAt, endsAt, now);
    }

    public void CloseEarly() => ClosedEarly = true;

    /// <summary>
    /// design-decisions.md "Donation-vs-Campaign-Close Consistency Boundary": checked ONLY at
    /// Donation-creation time, never at confirmation. A closed/expired campaign simply rejects new
    /// donations going forward - it never reaches back to reject/reverse an already-created Donation's
    /// later Finance-driven confirmation.
    /// </summary>
    public bool IsActive(DateTimeOffset now) => !ClosedEarly && now >= StartsAt && now < EndsAt;
}
