using System.Text.Json;
using UMS.Modules.Organization.Application.Abstractions;
using UMS.Modules.Organization.Application.Common;
using UMS.Modules.Organization.Domain.Common;
using UMS.Modules.Organization.Domain.Universities;
using UMS.Shared.Audit;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Organization.Application.Universities;

/// <summary>ORG-1: University CRUD (requirement-spec.md organization §2/§3/§6; ADR-0015's single-University assumption).</summary>
public sealed class UniversityService(
    IUniversityRepository universities,
    ICampusRepository campuses,
    IUnitOfWork unitOfWork,
    IAuditRecorder auditRecorder,
    IClock clock)
{
    public static UniversityDto ToDto(University university) =>
        new(university.Id.Value, university.Name, university.Code, university.Status.ToString(), university.CreatedAt, university.Version);

    public async Task<Result<UniversityDto>> CreateAsync(CreateUniversityRequest request, AuditContext audit, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Error.Validation("university.name_required", "University name is required.");
        }

        var now = clock.UtcNow;
        var university = University.Create(request.Name, request.Code, now);
        universities.Add(university);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var auditRequest = audit.ToRequest("University", university.Id.Value.ToString(), AuditActions.Create, beforeValueJson: null, afterValueJson: JsonSerializer.Serialize(new { name = university.Name, code = university.Code }));
        var commitResult = await TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, auditRequest, cancellationToken).ConfigureAwait(false);
        if (commitResult.IsFailure)
        {
            return commitResult.Error!;
        }

        return ToDto(university);
    }

    public async Task<Result<UniversityDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var university = await universities.GetByIdAsync(new UniversityId(id), cancellationToken).ConfigureAwait(false);
        return university is null
            ? Error.NotFound("university.not_found", $"No University exists with id '{id}'.")
            : ToDto(university);
    }

    public async Task<UniversityListPage> ListAsync(int skip, int take, CancellationToken cancellationToken = default)
    {
        skip = Math.Max(skip, 0);
        take = Math.Clamp(take, 1, 200);

        var items = await universities.ListAsync(skip, take, cancellationToken).ConfigureAwait(false);
        var total = await universities.CountAsync(cancellationToken).ConfigureAwait(false);
        return new UniversityListPage(items.Select(ToDto).ToList(), total, skip, take);
    }

    /// <summary>
    /// One `PATCH` covers a rename and/or an Active/Inactive transition (requirement-spec.md
    /// organization §6 names no separate deactivate endpoint for University). Deactivating locks
    /// this University row (`SELECT ... FOR UPDATE`) for the duration of the "any active Campus
    /// beneath it" cascade check, serializing against a concurrent `POST /campuses` targeting the
    /// same University (design-decisions.md, "Parent-Active-Status Validation Mechanism"; edge-
    /// cases.md's Faculty/Department analogue, generalized here to University/Campus).
    /// </summary>
    public async Task<Result<UniversityDto>> UpdateAsync(Guid id, UpdateUniversityRequest request, AuditContext audit, CancellationToken cancellationToken = default)
    {
        var wantsDeactivate = string.Equals(request.Status, nameof(NodeStatus.Inactive), StringComparison.OrdinalIgnoreCase);
        var wantsActivate = string.Equals(request.Status, nameof(NodeStatus.Active), StringComparison.OrdinalIgnoreCase);
        if (request.Status is not null && !wantsDeactivate && !wantsActivate)
        {
            return Error.Validation("university.invalid_status", "Status must be 'Active' or 'Inactive'.");
        }

        var university = wantsDeactivate
            ? await universities.GetByIdForUpdateAsync(new UniversityId(id), cancellationToken).ConfigureAwait(false)
            : await universities.GetByIdAsync(new UniversityId(id), cancellationToken).ConfigureAwait(false);
        if (university is null)
        {
            return Error.NotFound("university.not_found", $"No University exists with id '{id}'.");
        }

        var before = new { name = university.Name, code = university.Code, status = university.Status.ToString() };
        var now = clock.UtcNow;
        var renamed = false;

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            renamed = university.Rename(request.Name, now);
        }

        if (wantsDeactivate)
        {
            var hasActiveCampus = await campuses.HasAnyActiveUnderAsync(university.Id, cancellationToken).ConfigureAwait(false);
            if (hasActiveCampus)
            {
                return Error.Conflict("university.has_active_children", "Cannot deactivate a University that still has an active Campus - deactivate its Campuses first.");
            }

            university.Deactivate();
        }
        else if (wantsActivate)
        {
            university.Activate();
        }

        unitOfWork.SetExpectedVersion(university, request.Version);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var action = wantsDeactivate ? "deactivate" : wantsActivate ? "activate" : AuditActions.Update;
        var after = new { name = university.Name, code = university.Code, status = university.Status.ToString() };
        var auditRequest = audit.ToRequest("University", id.ToString(), action, JsonSerializer.Serialize(before), JsonSerializer.Serialize(after));

        var commitResult = await TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, auditRequest, cancellationToken).ConfigureAwait(false);
        if (commitResult.IsFailure)
        {
            return commitResult.Error!;
        }

        _ = renamed;
        return ToDto(university);
    }
}
