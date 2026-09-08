using UMS.Modules.Hostel.Domain.Common;

namespace UMS.Modules.Hostel.Domain.Events;

/// <summary>HOS-4: requirement-spec.md §3 - internal only, drives the Officer review queue.</summary>
public sealed record HostelApplicationRanked(Guid ApplicationId, decimal Score, int RankPosition, bool IsEligible, DateTimeOffset OccurredAt) : IDomainEvent;
