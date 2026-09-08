namespace UMS.Modules.Alumni.Domain.Jobs;

/// <summary>requirement-spec.md §2.3/§3: <c>Draft -&gt; PendingModeration -&gt; Published -&gt; Expired/Removed</c>.</summary>
public enum JobPostingStatus
{
    Draft,
    PendingModeration,
    Published,
    Expired,
    Removed,
}
