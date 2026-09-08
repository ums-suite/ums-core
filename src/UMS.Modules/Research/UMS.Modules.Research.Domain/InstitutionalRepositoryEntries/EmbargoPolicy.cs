using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Research.Domain.InstitutionalRepositoryEntries;

/// <summary>requirement-spec.md §3: <c>(isEmbargoed, embargoEndDate: nullable, accessLevel: Public | InstitutionalOnly | Restricted)</c>. An entry with <see cref="IsEmbargoed"/> true is excluded from every anonymous/public read regardless of <see cref="AccessLevel"/>, until the embargo lifts (§4).</summary>
public sealed record EmbargoPolicy
{
    private EmbargoPolicy(bool isEmbargoed, DateOnly? embargoEndDate, RepositoryAccessLevel accessLevel)
    {
        IsEmbargoed = isEmbargoed;
        EmbargoEndDate = embargoEndDate;
        AccessLevel = accessLevel;
    }

    public bool IsEmbargoed { get; }

    public DateOnly? EmbargoEndDate { get; }

    public RepositoryAccessLevel AccessLevel { get; }

    public static Result<EmbargoPolicy> Create(bool isEmbargoed, DateOnly? embargoEndDate, RepositoryAccessLevel accessLevel)
    {
        if (isEmbargoed && embargoEndDate is null)
        {
            return Error.Validation("embargopolicy.end_date_required", "An embargoed entry must have an EmbargoEndDate for the scheduled lift worker to act on.");
        }

        if (!isEmbargoed && embargoEndDate is not null)
        {
            return Error.Validation("embargopolicy.end_date_not_allowed", "A non-embargoed entry must not have an EmbargoEndDate.");
        }

        return new EmbargoPolicy(isEmbargoed, embargoEndDate, accessLevel);
    }

    /// <summary>Same policy, embargo lifted - used by both the daily scheduled worker and the explicit Admin early-override path (design-decisions.md).</summary>
    public EmbargoPolicy Lift() => new(false, null, AccessLevel);

    /// <summary>For infrastructure round-tripping of an already-validated stored value only.</summary>
    public static EmbargoPolicy FromStoredValue(bool isEmbargoed, DateOnly? embargoEndDate, RepositoryAccessLevel accessLevel) => new(isEmbargoed, embargoEndDate, accessLevel);
}
