namespace UMS.Modules.Career.Domain.Internships;

/// <summary>requirement-spec.md §2.2/§3: `Draft -> Published -> ApplicationsOpen -> ApplicationsClosed`, `Withdrawn` reachable from any non-terminal state.</summary>
public enum InternshipStatus
{
    Draft,
    Published,
    ApplicationsOpen,
    ApplicationsClosed,
    Withdrawn,
}
