namespace UMS.Modules.Academic.Domain.ResultPublications;

/// <summary>requirement-spec.md §4: "forward-only ... Draft -> Calculated -> Verified -> Approved -> Published -> Archived; the sole exception is the explicit correction cycle, which re-enters at Verified, never skips ahead to Published directly."</summary>
public enum ResultPublicationStatus
{
    Draft,
    Calculated,
    Verified,
    Approved,
    Published,
    Archived,
}
