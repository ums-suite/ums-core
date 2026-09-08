namespace UMS.Modules.Research.Application.Grants;

public sealed record GrantDto(
    Guid Id,
    string Title,
    string Description,
    Guid FundingBodyId,
    decimal FundingAmount,
    string Currency,
    DateOnly FundingPeriodStart,
    DateOnly FundingPeriodEnd,
    string Status,
    bool RequiresPiReassignment,
    bool IsPubliclyVisible,
    DateOnly? AwardDate,
    Guid? PrincipalInvestigatorFacultyMemberId,
    IReadOnlyList<GrantInvestigatorDto> Investigators,
    uint Version);

public sealed record GrantInvestigatorDto(Guid FacultyMemberId, string Role, DateTimeOffset AddedAt);

public sealed record GrantListPage(IReadOnlyList<GrantDto> Items, int Skip, int Take);

public sealed record ProposeGrantRequest(
    string Title,
    string Description,
    Guid FundingBodyId,
    decimal FundingAmount,
    string Currency,
    DateOnly FundingPeriodStart,
    DateOnly FundingPeriodEnd,
    Guid PrincipalInvestigatorFacultyMemberId);

public sealed record FundGrantRequest(
    DateOnly AwardDate,
    decimal ConfirmedFundingAmount,
    string ConfirmedCurrency,
    DateOnly ConfirmedFundingPeriodStart,
    DateOnly ConfirmedFundingPeriodEnd,
    uint Version);

public sealed record GrantVersionedActionRequest(uint Version);

public sealed record AddGrantInvestigatorRequest(Guid FacultyMemberId, string Role, uint Version);
