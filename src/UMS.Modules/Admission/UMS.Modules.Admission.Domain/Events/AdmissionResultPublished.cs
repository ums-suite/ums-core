using UMS.Modules.Admission.Domain.Common;

namespace UMS.Modules.Admission.Domain.Events;

/// <summary>requirement-spec.md §3: "Published transition" - consumed by Notifications, Documents, Audit, Reporting. Raised only once the PublishJob reports 100% cache-verified (design-decisions.md).</summary>
public sealed record AdmissionResultPublished(Guid AdmissionResultId, Guid CampaignId, int EntryCount, DateTimeOffset OccurredAt) : IDomainEvent;
