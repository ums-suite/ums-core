using UMS.Modules.Admission.Domain.Common;

namespace UMS.Modules.Admission.Domain.Events;

/// <summary>requirement-spec.md §3: "Verification complete" - consumed by Notifications, Audit.</summary>
public sealed record ApplicantRegistered(Guid ApplicantId, Guid IdentityUserId, DateTimeOffset OccurredAt) : IDomainEvent;
