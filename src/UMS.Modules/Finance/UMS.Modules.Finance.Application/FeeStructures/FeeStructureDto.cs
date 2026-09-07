namespace UMS.Modules.Finance.Application.FeeStructures;

public sealed record FeeStructureDto(
    Guid Id,
    string FeeType,
    string ApplicabilityType,
    Guid? ApplicabilityReferenceId,
    string? ApplicabilityServiceName,
    decimal Amount,
    string Currency,
    int VersionNumber,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    string Status,
    DateTimeOffset CreatedAt);

public sealed record CreateFeeStructureRequest(
    string FeeType,
    string ApplicabilityType,
    Guid? ApplicabilityReferenceId,
    string? ApplicabilityServiceName,
    decimal Amount,
    string? Currency,
    DateTimeOffset? EffectiveFrom);

public sealed record PublishNewFeeStructureVersionRequest(decimal Amount, string? Currency, DateTimeOffset? EffectiveFrom);
