using UMS.Modules.Admission.Domain.Common;

namespace UMS.Modules.Admission.Domain.Events;

/// <summary>requirement-spec.md §3: "Registrar approval" - consumed by Audit, (feeds AdmissionResult generation).</summary>
public sealed record MeritListApproved(Guid MeritListId, Guid CampaignId, DateTimeOffset OccurredAt) : IDomainEvent;
