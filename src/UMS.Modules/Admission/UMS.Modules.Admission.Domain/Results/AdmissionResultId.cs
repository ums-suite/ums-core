namespace UMS.Modules.Admission.Domain.Results;

/// <summary>
/// requirement-spec.md §2: "AdmissionResult follows the same controlled state machine shape as
/// ResultPublication (Draft → Calculated → Verified → Approved → Published → Archived)". This
/// build adds one intermediate state, <see cref="Publishing"/>, between <c>Approved</c> and
/// <c>Published</c> - design-decisions.md's own "Write-Through Cache Publish Atomicity" decision:
/// "AdmissionResult/MeritList in PostgreSQL exposes an intermediate Publishing status distinct from
/// Published; the transition to Published commits only once the PublishJob reports 100% of the
/// batch cache-verified. A reader observing Publishing is told the result isn't live yet."
/// </summary>
public enum AdmissionResultStatus
{
    Draft,
    Calculated,
    Verified,
    Approved,
    Publishing,
    Published,
    Archived,
}

public readonly record struct AdmissionResultId(Guid Value)
{
    public static AdmissionResultId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
