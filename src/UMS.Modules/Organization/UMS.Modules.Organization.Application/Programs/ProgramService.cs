using System.Text.Json;
using UMS.Modules.Organization.Application.Abstractions;
using UMS.Modules.Organization.Application.Common;
using UMS.Modules.Organization.Application.Hierarchy;
using UMS.Modules.Organization.Domain.Common;
using UMS.Modules.Organization.Domain.Departments;
using UMS.Shared.Audit;
using UMS.Shared.ErrorHandling.Results;
using OrgProgram = UMS.Modules.Organization.Domain.Programs.Program;

namespace UMS.Modules.Organization.Application.Programs;

/// <summary>ORG-5: Program record CRUD + deactivate, notifying Academic via `ProgramCreated` (requirement-spec.md organization §2/§3/§6/§7/§8/§9.1).</summary>
public sealed class ProgramService(
    IProgramRepository programs,
    IDepartmentRepository departments,
    IUnitOfWork unitOfWork,
    IAuditRecorder auditRecorder,
    IOrganizationTreeCache treeCache,
    HierarchyAncestryResolver ancestry,
    IClock clock)
{
    public static ProgramDto ToDto(OrgProgram program, string? languageCode) => new(
        program.Id.Value,
        program.DepartmentId.Value,
        program.Name,
        program.ResolveName(languageCode),
        program.Status.ToString(),
        program.CreatedAt,
        program.Version);

    /// <summary>edge-cases.md §8 "A Program created before its Department is fully configured": only the parent Department's existence and active status are validated - Program creation has no dependency on Faculty staffing at all.</summary>
    public async Task<Result<ProgramDto>> CreateAsync(CreateProgramRequest request, AuditContext audit, string? languageCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Error.Validation("program.name_required", "Program name is required.");
        }

        var department = await departments.GetByIdForUpdateAsync(new DepartmentId(request.DepartmentId), cancellationToken).ConfigureAwait(false);
        if (department is null)
        {
            return Error.NotFound("department.not_found", $"No Department exists with id '{request.DepartmentId}'.");
        }

        if (department.Status != NodeStatus.Active)
        {
            return Error.Validation("department.inactive", "Cannot create a Program under an inactive Department.");
        }

        var now = clock.UtcNow;
        var program = OrgProgram.Create(department.Id, request.Name, now);
        ApplyTranslations(program, request.Translations);
        programs.Add(program);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var auditRequest = audit.ToRequest("Program", program.Id.Value.ToString(), AuditActions.Create, null, JsonSerializer.Serialize(new { name = program.Name, departmentId = program.DepartmentId.Value }));
        var commitResult = await TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, auditRequest, cancellationToken).ConfigureAwait(false);
        if (commitResult.IsFailure)
        {
            return commitResult.Error!;
        }

        var departmentAncestors = await ancestry.GetDepartmentAncestorsAsync(department.Id, cancellationToken).ConfigureAwait(false);
        await treeCache.InvalidateAsync(program.Id.Value, [department.Id.Value, .. departmentAncestors], cancellationToken: cancellationToken).ConfigureAwait(false);

        return ToDto(program, languageCode);
    }

    public async Task<Result<ProgramDto>> GetByIdAsync(Guid id, string? languageCode, CancellationToken cancellationToken = default)
    {
        var program = await programs.GetByIdAsync(new Domain.Programs.ProgramId(id), cancellationToken).ConfigureAwait(false);
        return program is null
            ? Error.NotFound("program.not_found", $"No Program exists with id '{id}'.")
            : ToDto(program, languageCode);
    }

    public async Task<ProgramListPage> ListAsync(Guid? departmentId, int skip, int take, string? languageCode, CancellationToken cancellationToken = default)
    {
        skip = Math.Max(skip, 0);
        take = Math.Clamp(take, 1, 200);

        var departmentIdValue = departmentId.HasValue ? new DepartmentId(departmentId.Value) : (DepartmentId?)null;
        var items = await programs.ListAsync(departmentIdValue, skip, take, cancellationToken).ConfigureAwait(false);
        var total = await programs.CountAsync(departmentIdValue, cancellationToken).ConfigureAwait(false);
        return new ProgramListPage(items.Select(p => ToDto(p, languageCode)).ToList(), total, skip, take);
    }

    public async Task<Result<ProgramDto>> UpdateAsync(Guid id, UpdateProgramRequest request, AuditContext audit, string? languageCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Error.Validation("program.name_required", "Program name is required.");
        }

        var program = await programs.GetByIdAsync(new Domain.Programs.ProgramId(id), cancellationToken).ConfigureAwait(false);
        if (program is null)
        {
            return Error.NotFound("program.not_found", $"No Program exists with id '{id}'.");
        }

        var previousName = program.Name;
        var now = clock.UtcNow;
        var renamed = program.Rename(request.Name, now);
        ApplyTranslations(program, request.Translations);

        unitOfWork.SetExpectedVersion(program, request.Version);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var auditRequest = audit.ToRequest(
            "Program",
            id.ToString(),
            AuditActions.Update,
            JsonSerializer.Serialize(new { name = previousName }),
            JsonSerializer.Serialize(new { name = program.Name }));

        var commitResult = await TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, auditRequest, cancellationToken).ConfigureAwait(false);
        if (commitResult.IsFailure)
        {
            return commitResult.Error!;
        }

        if (renamed)
        {
            var ancestors = await ancestry.GetDepartmentAncestorsAsync(program.DepartmentId, cancellationToken).ConfigureAwait(false);
            await treeCache.InvalidateAsync(program.Id.Value, [program.DepartmentId.Value, .. ancestors], cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        return ToDto(program, languageCode);
    }

    /// <summary>Program is a leaf within Organization's own schema (Academic's Curriculum is a separate module, requirement-spec.md §9.1) - no local cascade-block check applies, only the version check.</summary>
    public async Task<Result<ProgramDto>> DeactivateAsync(Guid id, uint expectedVersion, AuditContext audit, CancellationToken cancellationToken = default)
    {
        var program = await programs.GetByIdAsync(new Domain.Programs.ProgramId(id), cancellationToken).ConfigureAwait(false);
        if (program is null)
        {
            return Error.NotFound("program.not_found", $"No Program exists with id '{id}'.");
        }

        if (program.Status == NodeStatus.Inactive)
        {
            return Error.Conflict("program.already_inactive", "Program is already inactive.");
        }

        var now = clock.UtcNow;
        program.Deactivate(now);
        unitOfWork.SetExpectedVersion(program, expectedVersion);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var auditRequest = audit.ToRequest("Program", id.ToString(), "deactivate", JsonSerializer.Serialize(new { status = nameof(NodeStatus.Active) }), JsonSerializer.Serialize(new { status = nameof(NodeStatus.Inactive) }));
        var commitResult = await TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, auditRequest, cancellationToken).ConfigureAwait(false);
        if (commitResult.IsFailure)
        {
            return commitResult.Error!;
        }

        var ancestors = await ancestry.GetDepartmentAncestorsAsync(program.DepartmentId, cancellationToken).ConfigureAwait(false);
        await treeCache.InvalidateAsync(program.Id.Value, [program.DepartmentId.Value, .. ancestors], cancellationToken: cancellationToken).ConfigureAwait(false);

        return ToDto(program, null);
    }

    private static void ApplyTranslations(OrgProgram program, IReadOnlyDictionary<string, string>? translations)
    {
        if (translations is null)
        {
            return;
        }

        foreach (var (languageCode, name) in translations)
        {
            program.SetTranslation(languageCode, name);
        }
    }
}
