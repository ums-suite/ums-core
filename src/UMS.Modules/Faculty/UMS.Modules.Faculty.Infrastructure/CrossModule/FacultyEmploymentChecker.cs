using Microsoft.EntityFrameworkCore;
using UMS.Modules.Faculty.Domain.FacultyMembers;
using UMS.Modules.Faculty.Infrastructure.Persistence;
using UMS.Shared.Faculty;

namespace UMS.Modules.Faculty.Infrastructure.CrossModule;

/// <summary>The real implementation of the promoted <see cref="IFacultyEmploymentChecker"/> contract - replaces Organization's former <c>StubFacultyEmploymentChecker</c> registration.</summary>
internal sealed class FacultyEmploymentChecker(FacultyDbContext context) : IFacultyEmploymentChecker
{
    public Task<bool> HasActiveFacultyMemberAsync(Guid departmentId, CancellationToken cancellationToken = default) =>
        context.FacultyMembers.AnyAsync(f => f.DepartmentId == departmentId && f.Status == FacultyMemberStatus.Active, cancellationToken);
}
