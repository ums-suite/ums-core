namespace UMS.Modules.Alumni.Domain.Donations;

/// <summary>design-decisions.md "Recurring-Donation Retry/Dunning Policy": pause on first failure, never an automated retry of that cycle.</summary>
public enum RecurrenceStatus
{
    Active,
    Paused,
    Cancelled,
}
