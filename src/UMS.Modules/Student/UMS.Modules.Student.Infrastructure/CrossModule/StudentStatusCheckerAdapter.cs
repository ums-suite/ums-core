using UMS.Modules.Student.Application.Abstractions;
using UMS.Modules.Student.Domain.Students;
using UMS.Shared.Student;

namespace UMS.Modules.Student.Infrastructure.CrossModule;

/// <summary>ACD-6: the one real implementation of <see cref="IStudentStatusChecker"/> - registered directly, no stub-then-promote dance needed (Student is the first mover for this contract, mirroring <c>FacultyMemberLookup</c>'s own remarks).</summary>
internal sealed class StudentStatusCheckerAdapter(IStudentRepository students) : IStudentStatusChecker
{
    public async Task<StudentAcademicStanding?> GetByStudentIdAsync(Guid studentId, CancellationToken cancellationToken = default)
    {
        var student = await students.GetByIdAsync(new StudentId(studentId), cancellationToken).ConfigureAwait(false);
        return student is null ? null : ToStanding(student);
    }

    public async Task<StudentAcademicStanding?> GetByUserIdAsync(Guid identityUserId, CancellationToken cancellationToken = default)
    {
        var student = await students.GetByUserIdAsync(identityUserId, cancellationToken).ConfigureAwait(false);
        return student is null ? null : ToStanding(student);
    }

    private static StudentAcademicStanding ToStanding(Domain.Students.Student student) =>
        new(student.Id.Value, student.DepartmentId, student.ProgramId, student.Status.ToString(), student.IdentityUserId);
}
