using UMS.Modules.Admission.Domain.Common;

namespace UMS.Modules.Admission.Domain.Events;

/// <summary>requirement-spec.md §3: "Confirmation fee + document verification complete" - consumed by Notifications, Audit. Student's own handoff is a direct command call (ADR-0003), never this event (see requirement-spec.md §2).</summary>
public sealed record AdmissionConfirmed(Guid ApplicationId, Guid ApplicantId, Guid CampaignId, DateTimeOffset OccurredAt) : IDomainEvent;
