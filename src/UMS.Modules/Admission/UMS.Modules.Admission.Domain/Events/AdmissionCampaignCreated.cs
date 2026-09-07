using UMS.Modules.Admission.Domain.Common;

namespace UMS.Modules.Admission.Domain.Events;

public sealed record AdmissionCampaignCreated(Guid CampaignId, string Name, DateTimeOffset OccurredAt) : IDomainEvent;
