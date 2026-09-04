using System.Text.Json;
using UMS.Modules.Student.Application.Abstractions;
using UMS.Modules.Student.Application.Common;
using UMS.Modules.Student.Domain.Students;
using UMS.Shared.Audit;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Student.Application.Students;

/// <summary>STU-5/STU-6/STU-7: self and admin/Registrar profile reads, plus the self-service-only field-scoped write (requirement-spec.md §6).</summary>
public sealed class StudentProfileService(
    IStudentRepository students,
    IUnitOfWork unitOfWork,
    IAuditRecorder auditRecorder)
{
    public async Task<Result<StudentDto>> GetOwnProfileAsync(Guid callerUserId, CancellationToken cancellationToken = default)
    {
        var student = await students.GetByUserIdAsync(callerUserId, cancellationToken).ConfigureAwait(false);
        return student is null
            ? Error.NotFound("student.not_found", "No Student profile is linked to your account.")
            : CreateStudentRecordService.ToDto(student);
    }

    public async Task<Result<StudentDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var student = await students.GetByIdAsync(new StudentId(id), cancellationToken).ConfigureAwait(false);
        return student is null
            ? Error.NotFound("student.not_found", $"No Student exists with id '{id}'.")
            : CreateStudentRecordService.ToDto(student);
    }

    /// <summary>STU-6: identity-bearing fields (legal name, DOB, national id) are not accepted here at all - requirement-spec.md §2/§9 decision 2 routes a change through a <c>StudentRequest</c> approval workflow, deliberately out of this Core pass's scope (release/DEVELOPMENT_PLAN.md row 11/16). There is no "reject" branch to write because those fields simply have no corresponding request field on <see cref="UpdateSelfServiceProfileRequest"/> - they cannot be submitted in the first place.</summary>
    public async Task<Result<StudentDto>> UpdateOwnProfileAsync(Guid callerUserId, UpdateSelfServiceProfileRequest request, AuditContext audit, CancellationToken cancellationToken = default)
    {
        var student = await students.GetByUserIdAsync(callerUserId, cancellationToken).ConfigureAwait(false);
        if (student is null)
        {
            return Error.NotFound("student.not_found", "No Student profile is linked to your account.");
        }

        var before = JsonSerializer.Serialize(new { contactEmail = student.ContactEmail, contactPhone = student.ContactPhone, photoUrl = student.PhotoUrl });
        student.UpdateSelfServiceProfile(request.ContactEmail, request.ContactPhone, request.PhotoUrl);
        unitOfWork.SetExpectedVersion(student, request.Version);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var auditRequest = audit.ToRequest(
            "Student",
            student.Id.Value.ToString(),
            AuditActions.Update,
            before,
            JsonSerializer.Serialize(new { contactEmail = student.ContactEmail, contactPhone = student.ContactPhone, photoUrl = student.PhotoUrl }),
            organizationScopeId: student.DepartmentId);

        var commitResult = await TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, auditRequest, cancellationToken).ConfigureAwait(false);
        return commitResult.IsFailure ? commitResult.Error! : CreateStudentRecordService.ToDto(student);
    }
}
