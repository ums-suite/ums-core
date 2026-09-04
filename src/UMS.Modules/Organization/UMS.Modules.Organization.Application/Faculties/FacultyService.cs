using System.Text.Json;
using UMS.Modules.Organization.Application.Abstractions;
using UMS.Modules.Organization.Application.Common;
using UMS.Modules.Organization.Application.Hierarchy;
using UMS.Modules.Organization.Domain.Campuses;
using UMS.Modules.Organization.Domain.Common;
using UMS.Modules.Organization.Domain.Faculties;
using UMS.Shared.Audit;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Organization.Application.Faculties;

/// <summary>ORG-3: Faculty CRUD + deactivate (requirement-spec.md organization §2/§3/§4/§6).</summary>
public sealed class FacultyService(
    IFacultyRepository faculties,
    ICampusRepository campuses,
    IDepartmentRepository departments,
    IUnitOfWork unitOfWork,
    IAuditRecorder auditRecorder,
    IOrganizationTreeCache treeCache,
    HierarchyAncestryResolver ancestry,
    IClock clock)
{
    public static FacultyDto ToDto(Faculty faculty, string? languageCode) => new(
        faculty.Id.Value,
        faculty.CampusId.Value,
        faculty.Name,
        faculty.ResolveName(languageCode),
        faculty.Status.ToString(),
        faculty.CreatedAt,
        faculty.Version);

    /// <summary>Locks the parent Campus row for the duration of the exists-and-active check plus this insert (design-decisions.md, "Parent-Active-Status Validation Mechanism"; edge-cases.md, "Faculty deactivation racing a concurrent Department creation beneath it" - generalized one level up, Campus/Faculty instead of Faculty/Department).</summary>
    public async Task<Result<FacultyDto>> CreateAsync(CreateFacultyRequest request, AuditContext audit, string? languageCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Error.Validation("faculty.name_required", "Faculty name is required.");
        }

        var campus = await campuses.GetByIdForUpdateAsync(new CampusId(request.CampusId), cancellationToken).ConfigureAwait(false);
        if (campus is null)
        {
            return Error.NotFound("campus.not_found", $"No Campus exists with id '{request.CampusId}'.");
        }

        if (campus.Status != NodeStatus.Active)
        {
            return Error.Validation("campus.inactive", "Cannot create a Faculty under an inactive Campus.");
        }

        var now = clock.UtcNow;
        var faculty = Faculty.Create(campus.Id, request.Name, now);
        ApplyTranslations(faculty, request.Translations);
        faculties.Add(faculty);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var auditRequest = audit.ToRequest("Faculty", faculty.Id.Value.ToString(), AuditActions.Create, null, JsonSerializer.Serialize(new { name = faculty.Name, campusId = faculty.CampusId.Value }));
        var commitResult = await TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, auditRequest, cancellationToken).ConfigureAwait(false);
        if (commitResult.IsFailure)
        {
            return commitResult.Error!;
        }

        var campusAncestors = await ancestry.GetCampusAncestorsAsync(campus.Id, cancellationToken).ConfigureAwait(false);
        await treeCache.InvalidateAsync(faculty.Id.Value, [campus.Id.Value, .. campusAncestors], cancellationToken: cancellationToken).ConfigureAwait(false);

        return ToDto(faculty, languageCode);
    }

    public async Task<Result<FacultyDto>> GetByIdAsync(Guid id, string? languageCode, CancellationToken cancellationToken = default)
    {
        var faculty = await faculties.GetByIdAsync(new FacultyId(id), cancellationToken).ConfigureAwait(false);
        return faculty is null
            ? Error.NotFound("faculty.not_found", $"No Faculty exists with id '{id}'.")
            : ToDto(faculty, languageCode);
    }

    public async Task<FacultyListPage> ListAsync(Guid? campusId, int skip, int take, string? languageCode, CancellationToken cancellationToken = default)
    {
        skip = Math.Max(skip, 0);
        take = Math.Clamp(take, 1, 200);

        var campusIdValue = campusId.HasValue ? new CampusId(campusId.Value) : (CampusId?)null;
        var items = await faculties.ListAsync(campusIdValue, skip, take, cancellationToken).ConfigureAwait(false);
        var total = await faculties.CountAsync(campusIdValue, cancellationToken).ConfigureAwait(false);
        return new FacultyListPage(items.Select(f => ToDto(f, languageCode)).ToList(), total, skip, take);
    }

    /// <summary>`PATCH` only renames (and/or updates translations) - status changes go exclusively through <see cref="DeactivateAsync"/>, matching requirement-spec.md organization §6's separate `POST /faculties/{id}/deactivate` route.</summary>
    public async Task<Result<FacultyDto>> UpdateAsync(Guid id, UpdateFacultyRequest request, AuditContext audit, string? languageCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Error.Validation("faculty.name_required", "Faculty name is required.");
        }

        var faculty = await faculties.GetByIdAsync(new FacultyId(id), cancellationToken).ConfigureAwait(false);
        if (faculty is null)
        {
            return Error.NotFound("faculty.not_found", $"No Faculty exists with id '{id}'.");
        }

        var previousName = faculty.Name;
        var now = clock.UtcNow;
        var renamed = faculty.Rename(request.Name, now);
        ApplyTranslations(faculty, request.Translations);

        unitOfWork.SetExpectedVersion(faculty, request.Version);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var auditRequest = audit.ToRequest(
            "Faculty",
            id.ToString(),
            AuditActions.Update,
            JsonSerializer.Serialize(new { name = previousName }),
            JsonSerializer.Serialize(new { name = faculty.Name }));

        var commitResult = await TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, auditRequest, cancellationToken).ConfigureAwait(false);
        if (commitResult.IsFailure)
        {
            return commitResult.Error!;
        }

        if (renamed)
        {
            var ancestors = await ancestry.GetFacultyAncestorsAsync(faculty.Id, cancellationToken).ConfigureAwait(false);
            var descendants = await ancestry.GetFacultyDescendantIdsAsync(faculty.Id, cancellationToken).ConfigureAwait(false);
            await treeCache.InvalidateAsync(faculty.Id.Value, ancestors, descendants, cancellationToken).ConfigureAwait(false);
        }

        return ToDto(faculty, languageCode);
    }

    /// <summary>ORG-3/requirement-spec.md organization §4: rejected outright if any active Department exists beneath this Faculty - the row lock is held across this check-plus-status-update (design-decisions.md).</summary>
    public async Task<Result<FacultyDto>> DeactivateAsync(Guid id, uint expectedVersion, AuditContext audit, CancellationToken cancellationToken = default)
    {
        var faculty = await faculties.GetByIdForUpdateAsync(new FacultyId(id), cancellationToken).ConfigureAwait(false);
        if (faculty is null)
        {
            return Error.NotFound("faculty.not_found", $"No Faculty exists with id '{id}'.");
        }

        if (faculty.Status == NodeStatus.Inactive)
        {
            return Error.Conflict("faculty.already_inactive", "Faculty is already inactive.");
        }

        var hasActiveDepartment = await departments.HasAnyActiveUnderAsync(faculty.Id, cancellationToken).ConfigureAwait(false);
        if (hasActiveDepartment)
        {
            return Error.Conflict("faculty.has_active_children", "Cannot deactivate a Faculty that still has an active Department - deactivate its Departments first.");
        }

        var now = clock.UtcNow;
        faculty.Deactivate(now);
        unitOfWork.SetExpectedVersion(faculty, expectedVersion);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var auditRequest = audit.ToRequest("Faculty", id.ToString(), "deactivate", JsonSerializer.Serialize(new { status = nameof(NodeStatus.Active) }), JsonSerializer.Serialize(new { status = nameof(NodeStatus.Inactive) }));
        var commitResult = await TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, auditRequest, cancellationToken).ConfigureAwait(false);
        if (commitResult.IsFailure)
        {
            return commitResult.Error!;
        }

        var ancestors = await ancestry.GetFacultyAncestorsAsync(faculty.Id, cancellationToken).ConfigureAwait(false);
        await treeCache.InvalidateAsync(faculty.Id.Value, ancestors, cancellationToken: cancellationToken).ConfigureAwait(false);

        return ToDto(faculty, null);
    }

    private static void ApplyTranslations(Faculty faculty, IReadOnlyDictionary<string, string>? translations)
    {
        if (translations is null)
        {
            return;
        }

        foreach (var (languageCode, name) in translations)
        {
            faculty.SetTranslation(languageCode, name);
        }
    }
}
