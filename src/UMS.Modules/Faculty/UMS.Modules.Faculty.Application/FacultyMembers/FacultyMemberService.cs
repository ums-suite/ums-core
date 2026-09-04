using System.Text.Json;
using UMS.Modules.Faculty.Application.Abstractions;
using UMS.Modules.Faculty.Application.Common;
using UMS.Modules.Faculty.Domain.FacultyMembers;
using UMS.Shared.Audit;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Faculty.Application.FacultyMembers;

/// <summary>FAC-1/FAC-2: FacultyMember employment-profile CRUD, onboarding, and status change (requirement-spec.md faculty §2 Employment Profile, §6).</summary>
public sealed class FacultyMemberService(
    IFacultyMemberRepository facultyMembers,
    IOrganizationDepartmentExistenceChecker departmentChecker,
    IUnitOfWork unitOfWork,
    IAuditRecorder auditRecorder,
    IClock clock)
{
    public static FacultyMemberDto ToDto(FacultyMember facultyMember) => new(
        facultyMember.Id.Value,
        facultyMember.UserId,
        facultyMember.EmployeeId,
        facultyMember.DepartmentId,
        facultyMember.DesignationId,
        facultyMember.EmploymentType.ToString(),
        facultyMember.Status.ToString(),
        facultyMember.IsDepartmentHead,
        facultyMember.ContactEmail,
        facultyMember.ContactPhone,
        facultyMember.JoiningDate,
        facultyMember.CreatedAt,
        facultyMember.Version);

    public async Task<Result<FacultyMemberDto>> OnboardAsync(OnboardFacultyMemberRequest request, AuditContext audit, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.EmployeeId))
        {
            return Error.Validation("facultymember.employee_id_required", "Employee id is required.");
        }

        if (!Enum.TryParse<EmploymentType>(request.EmploymentType, ignoreCase: true, out var employmentType))
        {
            return Error.Validation("facultymember.invalid_employment_type", $"'{request.EmploymentType}' is not a valid employment type.");
        }

        var departmentExists = await departmentChecker.ExistsAsync(request.DepartmentId, cancellationToken).ConfigureAwait(false);
        if (!departmentExists)
        {
            return Error.NotFound("department.not_found", $"No Department exists with id '{request.DepartmentId}'.");
        }

        var existing = await facultyMembers.GetByUserIdAsync(request.UserId, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return Error.Conflict("facultymember.already_onboarded", $"User '{request.UserId}' already has a FacultyMember profile.");
        }

        var now = clock.UtcNow;
        var facultyMember = FacultyMember.Onboard(request.UserId, request.EmployeeId, request.DepartmentId, request.DesignationId, employmentType, request.JoiningDate, now);
        facultyMembers.Add(facultyMember);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var auditRequest = audit.ToRequest(
            "FacultyMember",
            facultyMember.Id.Value.ToString(),
            AuditActions.Create,
            null,
            JsonSerializer.Serialize(new { employeeId = facultyMember.EmployeeId, departmentId = request.DepartmentId }));

        var commitResult = await TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, auditRequest, cancellationToken).ConfigureAwait(false);
        return commitResult.IsFailure ? commitResult.Error! : ToDto(facultyMember);
    }

    public async Task<Result<FacultyMemberDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var facultyMember = await facultyMembers.GetByIdAsync(new FacultyMemberId(id), cancellationToken).ConfigureAwait(false);
        return facultyMember is null
            ? Error.NotFound("facultymember.not_found", $"No FacultyMember exists with id '{id}'.")
            : ToDto(facultyMember);
    }

    public async Task<FacultyMemberListPage> ListAsync(Guid? departmentId, int skip, int take, CancellationToken cancellationToken = default)
    {
        skip = Math.Max(skip, 0);
        take = Math.Clamp(take, 1, 200);

        var items = await facultyMembers.ListAsync(departmentId, skip, take, cancellationToken).ConfigureAwait(false);
        var total = await facultyMembers.CountAsync(departmentId, cancellationToken).ConfigureAwait(false);
        return new FacultyMemberListPage(items.Select(ToDto).ToList(), total, skip, take);
    }

    /// <summary>edge-cases.md "FacultyMember Self-Service Update Racing an HR/Registrar Full Edit": field-scoped, ownership-checked write.</summary>
    public async Task<Result<FacultyMemberDto>> UpdateSelfServiceAsync(Guid id, Guid callerUserId, UpdateSelfServiceProfileRequest request, AuditContext audit, CancellationToken cancellationToken = default)
    {
        var facultyMember = await facultyMembers.GetByIdAsync(new FacultyMemberId(id), cancellationToken).ConfigureAwait(false);
        if (facultyMember is null)
        {
            return Error.NotFound("facultymember.not_found", $"No FacultyMember exists with id '{id}'.");
        }

        if (facultyMember.UserId != callerUserId)
        {
            return Error.Forbidden("facultymember.not_owner", "Only the owning FacultyMember may update their own self-service profile.");
        }

        var before = JsonSerializer.Serialize(new { contactEmail = facultyMember.ContactEmail, contactPhone = facultyMember.ContactPhone });
        facultyMember.UpdateSelfServiceProfile(request.ContactEmail, request.ContactPhone);
        unitOfWork.SetExpectedVersion(facultyMember, request.Version);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var auditRequest = audit.ToRequest(
            "FacultyMember",
            id.ToString(),
            AuditActions.Update,
            before,
            JsonSerializer.Serialize(new { contactEmail = facultyMember.ContactEmail, contactPhone = facultyMember.ContactPhone }));

        var commitResult = await TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, auditRequest, cancellationToken).ConfigureAwait(false);
        return commitResult.IsFailure ? commitResult.Error! : ToDto(facultyMember);
    }

    /// <summary>HR/Registrar full edit (requirement-spec.md §2, §16 role matrix).</summary>
    public async Task<Result<FacultyMemberDto>> UpdateEmploymentDetailsAsync(Guid id, UpdateEmploymentDetailsRequest request, AuditContext audit, CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<EmploymentType>(request.EmploymentType, ignoreCase: true, out var employmentType))
        {
            return Error.Validation("facultymember.invalid_employment_type", $"'{request.EmploymentType}' is not a valid employment type.");
        }

        var facultyMember = await facultyMembers.GetByIdAsync(new FacultyMemberId(id), cancellationToken).ConfigureAwait(false);
        if (facultyMember is null)
        {
            return Error.NotFound("facultymember.not_found", $"No FacultyMember exists with id '{id}'.");
        }

        var departmentExists = await departmentChecker.ExistsAsync(request.DepartmentId, cancellationToken).ConfigureAwait(false);
        if (!departmentExists)
        {
            return Error.NotFound("department.not_found", $"No Department exists with id '{request.DepartmentId}'.");
        }

        var before = JsonSerializer.Serialize(new
        {
            departmentId = facultyMember.DepartmentId,
            designationId = facultyMember.DesignationId,
            employmentType = facultyMember.EmploymentType.ToString(),
            isDepartmentHead = facultyMember.IsDepartmentHead,
        });

        facultyMember.UpdateEmploymentDetails(request.DepartmentId, request.DesignationId, employmentType, request.IsDepartmentHead, request.ContactEmail, request.ContactPhone);
        unitOfWork.SetExpectedVersion(facultyMember, request.Version);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var auditRequest = audit.ToRequest(
            "FacultyMember",
            id.ToString(),
            AuditActions.Update,
            before,
            JsonSerializer.Serialize(new
            {
                departmentId = facultyMember.DepartmentId,
                designationId = facultyMember.DesignationId,
                employmentType = facultyMember.EmploymentType.ToString(),
                isDepartmentHead = facultyMember.IsDepartmentHead,
            }));

        var commitResult = await TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, auditRequest, cancellationToken).ConfigureAwait(false);
        return commitResult.IsFailure ? commitResult.Error! : ToDto(facultyMember);
    }

    public async Task<Result<FacultyMemberDto>> ChangeStatusAsync(Guid id, ChangeFacultyMemberStatusRequest request, AuditContext audit, CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<FacultyMemberStatus>(request.Status, ignoreCase: true, out var newStatus))
        {
            return Error.Validation("facultymember.invalid_status", $"'{request.Status}' is not a valid FacultyMember status.");
        }

        var facultyMember = await facultyMembers.GetByIdAsync(new FacultyMemberId(id), cancellationToken).ConfigureAwait(false);
        if (facultyMember is null)
        {
            return Error.NotFound("facultymember.not_found", $"No FacultyMember exists with id '{id}'.");
        }

        if (facultyMember.Status == newStatus)
        {
            return Error.Conflict("facultymember.status_unchanged", $"FacultyMember is already {newStatus}.");
        }

        var previousStatus = facultyMember.Status;
        var now = clock.UtcNow;
        facultyMember.ChangeStatus(newStatus, now);
        unitOfWork.SetExpectedVersion(facultyMember, request.Version);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var auditRequest = audit.ToRequest(
            "FacultyMember",
            id.ToString(),
            "status_change",
            JsonSerializer.Serialize(new { status = previousStatus.ToString() }),
            JsonSerializer.Serialize(new { status = newStatus.ToString() }));

        var commitResult = await TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, auditRequest, cancellationToken).ConfigureAwait(false);
        return commitResult.IsFailure ? commitResult.Error! : ToDto(facultyMember);
    }
}
