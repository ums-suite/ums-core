namespace UMS.Modules.Library.Domain.Fines;

/// <summary>requirement-spec.md §2 Fine Accrual (per-day overdue rate) and §8 "a replacement-cost Fine is generated" for a lost copy.</summary>
public enum FineReason
{
    Overdue,
    LostReplacement,
}
