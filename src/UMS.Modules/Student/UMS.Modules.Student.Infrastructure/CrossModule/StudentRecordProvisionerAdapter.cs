using UMS.Modules.Student.Application.Common;
using UMS.Modules.Student.Application.Students;
using UMS.Shared.ErrorHandling.Results;
using UMS.Shared.Student;

namespace UMS.Modules.Student.Infrastructure.CrossModule;

/// <summary>
/// The one real implementation of <see cref="IStudentRecordProvisioner"/> (STU-1) - delegates to
/// Student's own <see cref="CreateStudentRecordService"/>, so a future Admission module (release/
/// DEVELOPMENT_PLAN.md Flow #15) never takes a forbidden dependency on <c>UMS.Modules.Student.*</c>
/// internals. Constructs a system-actor <see cref="AuditContext"/> (<see cref="AuditContext.ForSystem"/>)
/// since this call has no interactive HTTP user behind it - Admission's own confirmation step is
/// itself the actor, recorded under ADR-0012's <c>system:&lt;job-name&gt;</c> convention.
/// </summary>
internal sealed class StudentRecordProvisionerAdapter(CreateStudentRecordService createStudentRecordService) : IStudentRecordProvisioner
{
    public async Task<Result<StudentRecordSummary>> CreateAsync(CreateStudentRecordCommand command, CancellationToken cancellationToken = default)
    {
        var request = new CreateStudentRecordRequest(
            command.OriginatingApplicationId,
            command.AdmissionYear,
            command.FacultyCode,
            command.DepartmentId,
            command.ProgramId,
            command.GivenName,
            command.FamilyName,
            command.GivenNameBn,
            command.FamilyNameBn,
            command.Email,
            command.Mobile,
            command.DateOfBirth,
            command.NationalId);

        var audit = AuditContext.ForSystem(Guid.NewGuid().ToString());

        var result = await createStudentRecordService.CreateAsync(request, audit, cancellationToken).ConfigureAwait(false);
        return result.Match<Result<StudentRecordSummary>>(
            dto => new StudentRecordSummary(dto.Id, dto.StudentNumber, dto.IdentityUserId, dto.Status),
            error => error);
    }
}
