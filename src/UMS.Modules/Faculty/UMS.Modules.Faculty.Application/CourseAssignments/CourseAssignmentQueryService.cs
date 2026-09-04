using UMS.Modules.Faculty.Application.Abstractions;
using UMS.Modules.Faculty.Domain.CourseAssignments;

namespace UMS.Modules.Faculty.Application.CourseAssignments;

/// <summary>FAC-5: teaching-load / "Assigned Courses" read, served entirely from Faculty's own projection (requirement-spec.md faculty §2, §6).</summary>
public sealed class CourseAssignmentQueryService(ICourseAssignmentRepository courseAssignments)
{
    public static CourseAssignmentDto ToDto(CourseAssignment courseAssignment) => new(
        courseAssignment.Id.Value,
        courseAssignment.FacultyMemberId,
        courseAssignment.CourseOfferingId,
        courseAssignment.Status.ToString(),
        courseAssignment.AssignedAt,
        courseAssignment.EndedAt);

    public async Task<IReadOnlyList<CourseAssignmentDto>> ListByFacultyMemberAsync(Guid facultyMemberId, CancellationToken cancellationToken = default)
    {
        var items = await courseAssignments.ListByFacultyMemberAsync(facultyMemberId, cancellationToken).ConfigureAwait(false);
        return items.Select(ToDto).ToList();
    }
}
