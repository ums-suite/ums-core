namespace UMS.Modules.Hostel.Application.ApplicationWindows;

public sealed record ApplicationWindowDto(
    Guid Id,
    string SessionLabel,
    DateTimeOffset OpensAt,
    DateTimeOffset ClosesAt,
    IReadOnlyCollection<Guid> EligibleProgramIds,
    IReadOnlyCollection<int> EligibleYears,
    IReadOnlyCollection<EligibilityRuleDto> EligibilityRules,
    int RulesVersion,
    DateTimeOffset CreatedAt);

public sealed record EligibilityRuleDto(string RuleType, decimal Value, string Description);

public sealed record CreateApplicationWindowRequest(string SessionLabel, DateTimeOffset OpensAt, DateTimeOffset ClosesAt);

public sealed record ReplaceEligibleProgramsRequest(IReadOnlyCollection<Guid> ProgramIds);

public sealed record ReplaceEligibleYearsRequest(IReadOnlyCollection<int> Years);

public sealed record ReplaceEligibilityRulesRequest(IReadOnlyCollection<EligibilityRuleDto> Rules);
