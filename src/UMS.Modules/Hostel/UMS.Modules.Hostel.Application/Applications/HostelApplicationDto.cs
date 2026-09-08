namespace UMS.Modules.Hostel.Application.Applications;

public sealed record HostelApplicationDto(
    Guid Id,
    Guid StudentId,
    Guid ApplicationWindowId,
    string Status,
    IReadOnlyCollection<HostelPreferenceDto> Preferences,
    int YearOfStudy,
    bool HasFinancialNeed,
    decimal? HomeDistrictDistanceKm,
    decimal? EligibilityScore,
    bool? IsEligible,
    int? RankPosition,
    string? DecisionReason,
    Guid? AllocationId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? SubmittedAt);

public sealed record HostelPreferenceDto(Guid HostelId, string PreferredRoomType, int Rank);

public sealed record CreateHostelApplicationRequest(
    Guid ApplicationWindowId,
    int YearOfStudy,
    bool HasFinancialNeed,
    decimal? HomeDistrictDistanceKm,
    IReadOnlyCollection<HostelPreferenceDto> Preferences);

public sealed record ReviewHostelApplicationRequest(string Decision, string? Reason);
