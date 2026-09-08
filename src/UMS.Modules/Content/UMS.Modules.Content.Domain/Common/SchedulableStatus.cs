namespace UMS.Modules.Content.Domain.Common;

/// <summary>
/// requirement-spec.md §2.1/§2.3: the one `Draft -&gt; Scheduled -&gt; Published -&gt; Archived`
/// state machine shape shared by <see cref="Notices.Notice"/>, <see cref="Banners.Banner"/>, and
/// <see cref="Downloads.DownloadResource"/> ("one scheduling mechanism... applied to two [three]
/// entities, not bespoke implementations" - §2.3). <see cref="Events.Event"/> deliberately does NOT
/// use this (requirement-spec.md §9: "Event has no publish_at/expire_at state machine").
/// </summary>
public enum SchedulableStatus
{
    Draft,
    Scheduled,
    Published,
    Archived,
}
