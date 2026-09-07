using UMS.Modules.Finance.Application.Abstractions;
using UMS.Modules.Finance.Domain.Common;
using UMS.Modules.Finance.Domain.FeeStructures;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Finance.Application.FeeStructures;

/// <summary>FIN-1: FeeStructure CRUD + versioning (requirement-spec.md §2 Fee Structure Configuration).</summary>
public sealed class FeeStructureService(
    IFeeStructureRepository feeStructures,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<Result<FeeStructureDto>> CreateAsync(CreateFeeStructureRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var applicability = ParseApplicability(request.ApplicabilityType, request.ApplicabilityReferenceId, request.ApplicabilityServiceName);
        if (applicability.IsFailure)
        {
            return applicability.Error!;
        }

        var amount = Money.Create(request.Amount, request.Currency);
        if (amount.IsFailure)
        {
            return amount.Error!;
        }

        var now = clock.UtcNow;
        var existing = await feeStructures.GetActiveAsync(request.FeeType, applicability.Value.Type, applicability.Value.ReferenceId, applicability.Value.ServiceName, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return Error.Conflict("fee_structure.already_active", $"An Active FeeStructure already exists for feeType '{request.FeeType}' with this applicability - use the new-version endpoint to change its amount.");
        }

        var created = FeeStructure.CreateInitialVersion(request.FeeType, applicability.Value, amount.Value, request.EffectiveFrom ?? now, now);
        if (created.IsFailure)
        {
            return created.Error!;
        }

        feeStructures.Add(created.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDto(created.Value);
    }

    /// <summary>Deprecates the current Active version and publishes the next one, in the same transaction - requirement-spec.md §2: "a change to an in-effect FeeStructure never retroactively alters an already-generated Invoice".</summary>
    public async Task<Result<FeeStructureDto>> PublishNewVersionAsync(Guid currentFeeStructureId, PublishNewFeeStructureVersionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var current = await feeStructures.GetByIdAsync(new FeeStructureId(currentFeeStructureId), cancellationToken).ConfigureAwait(false);
        if (current is null)
        {
            return Error.NotFound("fee_structure.not_found", $"No FeeStructure exists with id '{currentFeeStructureId}'.");
        }

        var amount = Money.Create(request.Amount, request.Currency);
        if (amount.IsFailure)
        {
            return amount.Error!;
        }

        var now = clock.UtcNow;
        var effectiveFrom = request.EffectiveFrom ?? now;

        var next = current.CreateNewVersion(amount.Value, effectiveFrom, now);
        if (next.IsFailure)
        {
            return next.Error!;
        }

        var deprecated = current.Deprecate(effectiveFrom, now);
        if (deprecated.IsFailure)
        {
            return deprecated.Error!;
        }

        feeStructures.Add(next.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDto(next.Value);
    }

    public async Task<Result<IReadOnlyList<FeeStructureDto>>> ListAsync(CancellationToken cancellationToken = default)
    {
        var all = await feeStructures.GetAllAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyList<FeeStructureDto> dtos = all.Select(ToDto).ToList();
        return Result.Success(dtos);
    }

    internal static FeeStructureDto ToDto(FeeStructure feeStructure) => new(
        feeStructure.Id.Value,
        feeStructure.FeeType,
        feeStructure.Applicability.Type.ToString(),
        feeStructure.Applicability.ReferenceId,
        feeStructure.Applicability.ServiceName,
        feeStructure.Amount.Amount,
        feeStructure.Amount.Currency,
        feeStructure.VersionNumber,
        feeStructure.EffectiveFrom,
        feeStructure.EffectiveTo,
        feeStructure.Status.ToString(),
        feeStructure.CreatedAt);

    private static Result<FeeApplicability> ParseApplicability(string applicabilityType, Guid? referenceId, string? serviceName) =>
        applicabilityType?.Trim().ToLowerInvariant() switch
        {
            "program" => referenceId is Guid programId
                ? FeeApplicability.ForProgram(programId)
                : Error.Validation("fee_applicability.program_id_required", "A Program applicability requires applicabilityReferenceId."),
            "admissioncampaign" => referenceId is Guid campaignId
                ? FeeApplicability.ForAdmissionCampaign(campaignId)
                : Error.Validation("fee_applicability.admission_campaign_id_required", "An AdmissionCampaign applicability requires applicabilityReferenceId."),
            "service" => FeeApplicability.ForService(serviceName ?? string.Empty),
            _ => Error.Validation("fee_applicability.invalid_type", $"Unknown applicability type '{applicabilityType}' - expected 'Program', 'AdmissionCampaign', or 'Service'."),
        };
}
