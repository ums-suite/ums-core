using System.Text.Json;
using UMS.Modules.Organization.Application.Abstractions;
using UMS.Modules.Organization.Application.Common;
using UMS.Modules.Organization.Domain.Campuses;
using UMS.Modules.Organization.Domain.Common;
using UMS.Modules.Organization.Domain.Universities;
using UMS.Shared.Audit;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Organization.Application.Campuses;

/// <summary>ORG-2: Campus CRUD, validating the parent University exists and is active (requirement-spec.md organization §2/§4/§6).</summary>
public sealed class CampusService(
    ICampusRepository campuses,
    IUniversityRepository universities,
    IFacultyRepository faculties,
    IUnitOfWork unitOfWork,
    IAuditRecorder auditRecorder,
    IClock clock)
{
    public static CampusDto ToDto(Campus campus) =>
        new(campus.Id.Value, campus.UniversityId.Value, campus.Name, campus.Status.ToString(), campus.CreatedAt, campus.Version);

    /// <summary>
    /// design-decisions.md, "Parent-Active-Status Validation Mechanism": locks the parent
    /// University row (`SELECT ... FOR UPDATE`) for the duration of the exists-and-active check
    /// plus this insert, serializing against a concurrent `PATCH /universities/{id}` deactivation
    /// targeting the same University (edge-cases.md's Faculty/Department race, generalized one
    /// level up).
    /// </summary>
    public async Task<Result<CampusDto>> CreateAsync(CreateCampusRequest request, AuditContext audit, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Error.Validation("campus.name_required", "Campus name is required.");
        }

        var university = await universities.GetByIdForUpdateAsync(new UniversityId(request.UniversityId), cancellationToken).ConfigureAwait(false);
        if (university is null)
        {
            return Error.NotFound("university.not_found", $"No University exists with id '{request.UniversityId}'.");
        }

        if (university.Status != NodeStatus.Active)
        {
            return Error.Validation("university.inactive", "Cannot create a Campus under an inactive University.");
        }

        var now = clock.UtcNow;
        var campus = Campus.Create(university.Id, request.Name, now);
        campuses.Add(campus);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var auditRequest = audit.ToRequest("Campus", campus.Id.Value.ToString(), AuditActions.Create, null, JsonSerializer.Serialize(new { name = campus.Name, universityId = campus.UniversityId.Value }));
        var commitResult = await TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, auditRequest, cancellationToken).ConfigureAwait(false);
        if (commitResult.IsFailure)
        {
            return commitResult.Error!;
        }

        return ToDto(campus);
    }

    public async Task<Result<CampusDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var campus = await campuses.GetByIdAsync(new CampusId(id), cancellationToken).ConfigureAwait(false);
        return campus is null
            ? Error.NotFound("campus.not_found", $"No Campus exists with id '{id}'.")
            : ToDto(campus);
    }

    public async Task<CampusListPage> ListAsync(Guid? universityId, int skip, int take, CancellationToken cancellationToken = default)
    {
        skip = Math.Max(skip, 0);
        take = Math.Clamp(take, 1, 200);

        var universityIdValue = universityId.HasValue ? new UniversityId(universityId.Value) : (UniversityId?)null;
        var items = await campuses.ListAsync(universityIdValue, skip, take, cancellationToken).ConfigureAwait(false);
        var total = await campuses.CountAsync(universityIdValue, cancellationToken).ConfigureAwait(false);
        return new CampusListPage(items.Select(ToDto).ToList(), total, skip, take);
    }

    /// <summary>Deactivating locks this Campus row for the "any active Faculty beneath it" cascade check, serializing against a concurrent `POST /faculties` under the same Campus (same pattern as <see cref="Universities.UniversityService.UpdateAsync"/>).</summary>
    public async Task<Result<CampusDto>> UpdateAsync(Guid id, UpdateCampusRequest request, AuditContext audit, CancellationToken cancellationToken = default)
    {
        var wantsDeactivate = string.Equals(request.Status, nameof(NodeStatus.Inactive), StringComparison.OrdinalIgnoreCase);
        var wantsActivate = string.Equals(request.Status, nameof(NodeStatus.Active), StringComparison.OrdinalIgnoreCase);
        if (request.Status is not null && !wantsDeactivate && !wantsActivate)
        {
            return Error.Validation("campus.invalid_status", "Status must be 'Active' or 'Inactive'.");
        }

        var campus = wantsDeactivate
            ? await campuses.GetByIdForUpdateAsync(new CampusId(id), cancellationToken).ConfigureAwait(false)
            : await campuses.GetByIdAsync(new CampusId(id), cancellationToken).ConfigureAwait(false);
        if (campus is null)
        {
            return Error.NotFound("campus.not_found", $"No Campus exists with id '{id}'.");
        }

        var before = new { name = campus.Name, status = campus.Status.ToString() };
        var now = clock.UtcNow;

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            campus.Rename(request.Name, now);
        }

        if (wantsDeactivate)
        {
            var hasActiveFaculty = await faculties.HasAnyActiveUnderAsync(campus.Id, cancellationToken).ConfigureAwait(false);
            if (hasActiveFaculty)
            {
                return Error.Conflict("campus.has_active_children", "Cannot deactivate a Campus that still has an active Faculty - deactivate its Faculties first.");
            }

            campus.Deactivate();
        }
        else if (wantsActivate)
        {
            campus.Activate();
        }

        unitOfWork.SetExpectedVersion(campus, request.Version);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var action = wantsDeactivate ? "deactivate" : wantsActivate ? "activate" : AuditActions.Update;
        var after = new { name = campus.Name, status = campus.Status.ToString() };
        var auditRequest = audit.ToRequest("Campus", id.ToString(), action, JsonSerializer.Serialize(before), JsonSerializer.Serialize(after));

        var commitResult = await TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, auditRequest, cancellationToken).ConfigureAwait(false);
        return commitResult.IsFailure ? commitResult.Error! : ToDto(campus);
    }
}
