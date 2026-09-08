namespace UMS.Modules.Alumni.Domain.Donations;

/// <summary>design-decisions.md "Donation Confirmation Consistency Model": Pending until Finance's PaymentCompleted/PaymentFailed signal - never a speculative "probably confirmed" state.</summary>
public enum DonationStatus
{
    Pending,
    Confirmed,
    Failed,
}
