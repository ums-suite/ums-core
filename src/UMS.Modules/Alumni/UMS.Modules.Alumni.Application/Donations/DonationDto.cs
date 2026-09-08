namespace UMS.Modules.Alumni.Application.Donations;

public sealed record DonationDto(
    Guid Id,
    Guid AlumnusId,
    Guid CampaignId,
    decimal Amount,
    string Currency,
    bool IsAnonymous,
    string RecurrenceInterval,
    string? RecurrenceStatus,
    Guid? SeriesRootDonationId,
    string Status,
    Guid? InvoiceId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ConfirmedAt,
    DateTimeOffset? NextChargeAt,
    uint Version);

public sealed record InitiateDonationRequest(Guid CampaignId, decimal Amount, string Currency, bool IsAnonymous, string RecurrenceInterval);

public sealed record DonationCampaignDto(Guid Id, string Name, string? Description, decimal GoalAmount, string Currency, DateTimeOffset StartsAt, DateTimeOffset EndsAt, bool ClosedEarly, DateTimeOffset CreatedAt);

public sealed record CreateDonationCampaignRequest(string Name, string? Description, decimal GoalAmount, string Currency, DateTimeOffset StartsAt, DateTimeOffset EndsAt);
