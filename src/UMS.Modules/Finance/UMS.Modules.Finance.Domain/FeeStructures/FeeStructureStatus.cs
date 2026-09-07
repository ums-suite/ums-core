namespace UMS.Modules.Finance.Domain.FeeStructures;

/// <summary>requirement-spec.md finance §2: "versioned - a change to an in-effect FeeStructure never retroactively alters an already-generated Invoice." Exactly one <see cref="Active"/> row exists per (FeeType, Applicability) at a time (FeeStructureConfiguration's own partial unique index); publishing a new version deprecates the old one in the same transaction.</summary>
public enum FeeStructureStatus
{
    Active,
    Deprecated,
}
