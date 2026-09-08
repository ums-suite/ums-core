using UMS.Modules.Content.Domain.Common;

namespace UMS.Modules.Content.Domain.Events;

/// <summary>CNT-9: requirement-spec.md §3 - internal to Content/CDN layer, the public-cache-invalidation trigger (design-decisions.md "Cache-Correctness Backstop" - best-effort, never load-bearing).</summary>
public sealed record BannerPublished(Guid BannerId, DateTimeOffset OccurredAt) : IDomainEvent;
