using UMS.Modules.Admission.Domain.Common;

namespace UMS.Modules.Admission.Domain.Events;

public sealed record MeritListGenerated(Guid MeritListId, Guid CampaignId, int EntryCount, DateTimeOffset OccurredAt) : IDomainEvent;
