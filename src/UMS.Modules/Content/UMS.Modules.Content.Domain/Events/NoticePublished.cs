using UMS.Modules.Content.Domain.Common;

namespace UMS.Modules.Content.Domain.Events;

/// <summary>CNT-1/CNT-13: requirement-spec.md §3 - consumers: Notifications (urgent-notice fan-out per audience, only when <see cref="IsUrgent"/>), Reporting.</summary>
public sealed record NoticePublished(Guid NoticeId, bool IsUrgent, ContentAudience Audience, Guid? OrganizationNodeId, DateTimeOffset OccurredAt) : IDomainEvent;
