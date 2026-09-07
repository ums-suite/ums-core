using UMS.Modules.Finance.Domain.Common;
using UMS.Modules.Finance.Domain.Events;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Finance.Domain.FeeStructures;

/// <summary>
/// FIN-1: a fee's type/amount/applicability, own lifecycle (create/version/deprecate), no parent
/// aggregate (requirement-spec.md finance §3).
///
/// <para>
/// <b>Versioning mechanism.</b> Each row IS one version - there is no separate "FeeStructureFamily"
/// wrapper type. <see cref="CreateNewVersion"/> returns a brand-new <see cref="FeeStructure"/> (the
/// next <see cref="VersionNumber"/> for the same <see cref="FeeType"/>/<see cref="Applicability"/>
/// pair) while the caller (<c>FeeStructureService</c>) deprecates the prior <see cref="Active"/> row
/// in the SAME transaction (FeeStructureConfiguration's partial unique index keeps exactly one
/// <see cref="Active"/> row per pair at the database level too). requirement-spec.md §2's own
/// invariant - "a change to an in-effect FeeStructure never retroactively alters an already-generated
/// Invoice" - is enforced by <see cref="Invoices.Invoice"/> snapshotting <see cref="Id"/> AND
/// <see cref="VersionNumber"/> AND the amount itself at creation time, never following a live
/// reference back to this row.
/// </para>
/// </summary>
public sealed class FeeStructure : AggregateRoot<FeeStructureId>
{
    private FeeStructure()
    {
    }

    private FeeStructure(FeeStructureId id, string feeType, FeeApplicability applicability, Money amount, int versionNumber, DateTimeOffset effectiveFrom, DateTimeOffset now)
    {
        Id = id;
        FeeType = feeType;
        ApplicabilityType = applicability.Type;
        ApplicabilityReferenceId = applicability.ReferenceId;
        ApplicabilityServiceName = applicability.ServiceName;
        Amount = amount;
        VersionNumber = versionNumber;
        EffectiveFrom = effectiveFrom;
        Status = FeeStructureStatus.Active;
        CreatedAt = now;
    }

    public string FeeType { get; private set; } = string.Empty;

    /// <summary>
    /// Stored as three flat scalars (<see cref="ApplicabilityType"/>/<see cref="ApplicabilityReferenceId"/>/
    /// <see cref="ApplicabilityServiceName"/>), not a nested value object - FeeStructureConfiguration's
    /// own remarks explain why: this version's EF Core has no way to compose a unique index
    /// spanning both an owner scalar (<see cref="FeeType"/>) and a nested complex-property/owned-type
    /// member together. This computed property reconstructs the value object for any caller that
    /// wants the validated, behavior-bearing shape back.
    /// </summary>
    public FeeApplicability Applicability => FeeApplicability.FromStoredValue(ApplicabilityType, ApplicabilityReferenceId, ApplicabilityServiceName);

    public FeeApplicabilityType ApplicabilityType { get; private set; }

    public Guid? ApplicabilityReferenceId { get; private set; }

    public string? ApplicabilityServiceName { get; private set; }

    public Money Amount { get; private set; }

    public int VersionNumber { get; private set; }

    public DateTimeOffset EffectiveFrom { get; private set; }

    public DateTimeOffset? EffectiveTo { get; private set; }

    public FeeStructureStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>The first version for a brand-new (FeeType, Applicability) pair.</summary>
    public static Result<FeeStructure> CreateInitialVersion(string feeType, FeeApplicability applicability, Money amount, DateTimeOffset effectiveFrom, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(applicability);

        if (string.IsNullOrWhiteSpace(feeType))
        {
            return Error.Validation("fee_structure.fee_type_required", "A FeeStructure's feeType is required.");
        }

        var structure = new FeeStructure(FeeStructureId.New(), feeType.Trim(), applicability, amount, versionNumber: 1, effectiveFrom, now);
        structure.Raise(new FeeStructurePublished(structure.Id.Value, structure.FeeType, structure.VersionNumber, amount.Amount, now));
        return structure;
    }

    public bool IsEffectiveAt(DateTimeOffset asOf) =>
        Status == FeeStructureStatus.Active && asOf >= EffectiveFrom && (EffectiveTo is null || asOf < EffectiveTo.Value);

    /// <summary>Publishes the next version of THIS FeeStructure's (FeeType, Applicability) pair. Caller must separately <see cref="Deprecate"/> this instance in the same transaction.</summary>
    public Result<FeeStructure> CreateNewVersion(Money newAmount, DateTimeOffset effectiveFrom, DateTimeOffset now)
    {
        if (Status != FeeStructureStatus.Active)
        {
            return Error.Conflict("fee_structure.not_active", $"FeeStructure '{Id}' is not Active and cannot be superseded by a new version.");
        }

        if (effectiveFrom < EffectiveFrom)
        {
            return Error.Validation("fee_structure.effective_from_before_current", "A new FeeStructure version's effectiveFrom must not be earlier than the current version's own effectiveFrom.");
        }

        var next = new FeeStructure(FeeStructureId.New(), FeeType, Applicability, newAmount, VersionNumber + 1, effectiveFrom, now);
        next.Raise(new FeeStructurePublished(next.Id.Value, next.FeeType, next.VersionNumber, newAmount.Amount, now));
        return next;
    }

    public Result Deprecate(DateTimeOffset effectiveTo, DateTimeOffset now)
    {
        if (Status != FeeStructureStatus.Active)
        {
            return Result.Failure(Error.Conflict("fee_structure.not_active", $"FeeStructure '{Id}' is already Deprecated."));
        }

        Status = FeeStructureStatus.Deprecated;
        EffectiveTo = effectiveTo;
        _ = now;
        return Result.Success();
    }
}
