namespace UMS.Modules.Career.Domain.Drives;

/// <summary>requirement-spec.md §2.3/§3: `Draft -> Scheduled -> RegistrationOpen -> RegistrationClosed -> Completed`, `Cancelled` reachable from any non-terminal state.</summary>
public enum DriveStatus
{
    Draft,
    Scheduled,
    RegistrationOpen,
    RegistrationClosed,
    Completed,
    Cancelled,
}
