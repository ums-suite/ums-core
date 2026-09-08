namespace UMS.Modules.Hostel.Domain.Allocations;

/// <summary>
/// requirement-spec.md §3: <c>Pending -&gt; FeePaid -&gt; Active -&gt; CheckedOut</c>, plus
/// <see cref="Expired"/> - HOS-10's fee-grace-period auto-expiry (§8 edge case "fails to pay the fee
/// within the configured grace period -&gt; Allocation auto-expires back to the bed pool"), a distinct
/// terminal state from <see cref="CheckedOut"/> since an expired Allocation was never actually used.
/// Both <see cref="CheckedOut"/> and <see cref="Expired"/> are terminal (requirement-spec.md §4:
/// "Once CheckedOut, an Allocation is terminal").
/// </summary>
public enum AllocationStatus
{
    Pending,
    FeePaid,
    Active,
    CheckedOut,
    Expired,
}
