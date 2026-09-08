namespace UMS.Modules.Research.Application.FundingBodies;

public sealed record FundingBodyDto(Guid Id, string Name, string Country, string Type, string? Website, DateTimeOffset CreatedAt);

public sealed record FundingBodyListPage(IReadOnlyList<FundingBodyDto> Items, int Skip, int Take);

public sealed record CreateFundingBodyRequest(string Name, string Country, string Type, string? Website);

public sealed record UpdateFundingBodyRequest(string Name, string Country, string Type, string? Website);
