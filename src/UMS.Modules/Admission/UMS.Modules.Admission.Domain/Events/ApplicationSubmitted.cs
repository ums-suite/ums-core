using UMS.Modules.Admission.Domain.Common;

namespace UMS.Modules.Admission.Domain.Events;

/// <summary>requirement-spec.md §3: "Submit gate passes" - consumed by Notifications, Audit, Documents (admit card, ADM-9).</summary>
public sealed record ApplicationSubmitted(Guid ApplicationId, Guid ApplicantId, Guid CampaignId, string ApplicationNumber, DateTimeOffset OccurredAt) : IDomainEvent;
