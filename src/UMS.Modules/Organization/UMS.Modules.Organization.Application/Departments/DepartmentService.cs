using System.Text.Json;
using UMS.Modules.Organization.Application.Abstractions;
using UMS.Modules.Organization.Application.Common;
using UMS.Modules.Organization.Application.Hierarchy;
using UMS.Modules.Organization.Domain.Common;
using UMS.Modules.Organization.Domain.Departments;
using UMS.Modules.Organization.Domain.Faculties;
using UMS.Shared.Audit;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Organization.Application.Departments;

/// <summary>ORG-4: Department CRUD + deactivate, rejecting creation under an inactive Faculty (requirement-spec.md organization §2/§3/§4/§6/§8).</summary>
public sealed class DepartmentService(
    IDepartmentRepository departments,
    IFacultyRepository faculties,
    IProgramRepository programs,
    IFacultyEmploymentChecker employmentChecker,
    IUnitOfWork unitOfWork,
    IAuditRecorder auditRecorder,
    IOrganizationTreeCache treeCache,
    HierarchyAncestryResolver ancestry,
    IClock clock)
{
    public static DepartmentDto ToDto(Department department, string? languageCode) => new(
        department.Id.Value,
        department.FacultyId.Value,
        department.Name,
        department.ResolveName(languageCode),
        department.Status.ToString(),
        department.CreatedAt,
        department.Version);

    public async Task<Result<DepartmentDto>> CreateAsync(CreateDepartmentRequest request, AuditContext audit, string? languageCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Error.Validation("department.name_required", "Department name is required.");
        }

        var faculty = await faculties.GetByIdForUpdateAsync(new FacultyId(request.FacultyId), cancellationToken).ConfigureAwait(false);
        if (faculty is null)
        {
            return Error.NotFound("faculty.not_found", $"No Faculty exists with id '{request.FacultyId}'.");
        }

        if (faculty.Status != NodeStatus.Active)
        {
            return Error.Validation("faculty.inactive", "Cannot create a Department under an inactive Faculty.");
        }

        var now = clock.UtcNow;
        var department = Department.Create(faculty.Id, request.Name, now);
        ApplyTranslations(department, request.Translations);
        departments.Add(department);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var auditRequest = audit.ToRequest("Department", department.Id.Value.ToString(), AuditActions.Create, null, JsonSerializer.Serialize(new { name = department.Name, facultyId = department.FacultyId.Value }));
        var commitResult = await TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, auditRequest, cancellationToken).ConfigureAwait(false);
        if (commitResult.IsFailure)
        {
            return commitResult.Error!;
        }

        var facultyAncestors = await ancestry.GetFacultyAncestorsAsync(faculty.Id, cancellationToken).ConfigureAwait(false);
        await treeCache.InvalidateAsync(department.Id.Value, [faculty.Id.Value, .. facultyAncestors], cancellationToken: cancellationToken).ConfigureAwait(false);

        return ToDto(department, languageCode);
    }

    public async Task<Result<DepartmentDto>> GetByIdAsync(Guid id, string? languageCode, CancellationToken cancellationToken = default)
    {
        var department = await departments.GetByIdAsync(new DepartmentId(id), cancellationToken).ConfigureAwait(false);
        return department is null
            ? Error.NotFound("department.not_found", $"No Department exists with id '{id}'.")
            : ToDto(department, languageCode);
    }

    public async Task<DepartmentListPage> ListAsync(Guid? facultyId, int skip, int take, string? languageCode, CancellationToken cancellationToken = default)
    {
        skip = Math.Max(skip, 0);
        take = Math.Clamp(take, 1, 200);

        var facultyIdValue = facultyId.HasValue ? new FacultyId(facultyId.Value) : (FacultyId?)null;
        var items = await departments.ListAsync(facultyIdValue, skip, take, cancellationToken).ConfigureAwait(false);
        var total = await departments.CountAsync(facultyIdValue, cancellationToken).ConfigureAwait(false);
        return new DepartmentListPage(items.Select(d => ToDto(d, languageCode)).ToList(), total, skip, take);
    }

    public async Task<Result<DepartmentDto>> UpdateAsync(Guid id, UpdateDepartmentRequest request, AuditContext audit, string? languageCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Error.Validation("department.name_required", "Department name is required.");
        }

        var department = await departments.GetByIdAsync(new DepartmentId(id), cancellationToken).ConfigureAwait(false);
        if (department is null)
        {
            return Error.NotFound("department.not_found", $"No Department exists with id '{id}'.");
        }

        var previousName = department.Name;
        var now = clock.UtcNow;
        var renamed = department.Rename(request.Name, now);
        ApplyTranslations(department, request.Translations);

        unitOfWork.SetExpectedVersion(department, request.Version);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var auditRequest = audit.ToRequest(
            "Department",
            id.ToString(),
            AuditActions.Update,
            JsonSerializer.Serialize(new { name = previousName }),
            JsonSerializer.Serialize(new { name = department.Name }));

        var commitResult = await TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, auditRequest, cancellationToken).ConfigureAwait(false);
        if (commitResult.IsFailure)
        {
            return commitResult.Error!;
        }

        if (renamed)
        {
            var ancestors = await ancestry.GetDepartmentAncestorsAsync(department.Id, cancellationToken).ConfigureAwait(false);
            var descendants = await ancestry.GetDepartmentDescendantIdsAsync(department.Id, cancellationToken).ConfigureAwait(false);
            await treeCache.InvalidateAsync(department.Id.Value, ancestors, descendants, cancellationToken).ConfigureAwait(false);
        }

        return ToDto(department, languageCode);
    }

    /// <summary>
    /// ORG-4/edge-cases.md "Deactivating a Department that still has a Faculty member on record":
    /// checks both the local "any active Program beneath it" invariant and Faculty's own (stub
    /// until Flow #10) employment-check interface, both under this Department's row lock, and
    /// re-checks the employment interface immediately before commit (design-decisions.md,
    /// "Cross-Module Hard-Delete Reference Check Timing" - the same resolution pattern applied to
    /// a deactivation instead of a hard delete, per that decision's own cross-reference).
    /// </summary>
    public async Task<Result<DepartmentDto>> DeactivateAsync(Guid id, uint expectedVersion, AuditContext audit, CancellationToken cancellationToken = default)
    {
        var department = await departments.GetByIdForUpdateAsync(new DepartmentId(id), cancellationToken).ConfigureAwait(false);
        if (department is null)
        {
            return Error.NotFound("department.not_found", $"No Department exists with id '{id}'.");
        }

        if (department.Status == NodeStatus.Inactive)
        {
            return Error.Conflict("department.already_inactive", "Department is already inactive.");
        }

        var hasActiveProgram = await programs.ListAsync(department.Id, 0, 1, cancellationToken).ConfigureAwait(false);
        if (hasActiveProgram.Any(p => p.Status == NodeStatus.Active))
        {
            return Error.Conflict("department.has_active_children", "Cannot deactivate a Department that still has an active Program - deactivate its Programs first.");
        }

        // Re-checked immediately before commit, not just once upfront - see this method's own
        // remarks. A true cross-module TOCTOU race (a transfer landing between this check and
        // commit) is a documented, accepted residual risk (edge-cases.md), not something this
        // re-check eliminates outright.
        var hasActiveFacultyMember = await employmentChecker.HasActiveFacultyMemberAsync(id, cancellationToken).ConfigureAwait(false);
        if (hasActiveFacultyMember)
        {
            return Error.Conflict("department.has_active_faculty_member", "Cannot deactivate a Department that still has an active FacultyMember on record - transfer them first.");
        }

        var now = clock.UtcNow;
        department.Deactivate(now);
        unitOfWork.SetExpectedVersion(department, expectedVersion);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var auditRequest = audit.ToRequest("Department", id.ToString(), "deactivate", JsonSerializer.Serialize(new { status = nameof(NodeStatus.Active) }), JsonSerializer.Serialize(new { status = nameof(NodeStatus.Inactive) }));
        var commitResult = await TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, auditRequest, cancellationToken).ConfigureAwait(false);
        if (commitResult.IsFailure)
        {
            return commitResult.Error!;
        }

        var ancestors = await ancestry.GetDepartmentAncestorsAsync(department.Id, cancellationToken).ConfigureAwait(false);
        await treeCache.InvalidateAsync(department.Id.Value, ancestors, cancellationToken: cancellationToken).ConfigureAwait(false);

        return ToDto(department, null);
    }

    private static void ApplyTranslations(Department department, IReadOnlyDictionary<string, string>? translations)
    {
        if (translations is null)
        {
            return;
        }

        foreach (var (languageCode, name) in translations)
        {
            department.SetTranslation(languageCode, name);
        }
    }
}
