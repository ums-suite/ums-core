using Microsoft.EntityFrameworkCore;
using UMS.Modules.Faculty.Infrastructure.Persistence;
using UMS.Shared.Faculty;

namespace UMS.Modules.Faculty.Infrastructure.CrossModule;

/// <summary>FAC-3: the one real implementation of <see cref="IFacultyMemberLookup"/> - Academic (Flow #12, doesn't exist yet) will resolve this in-process once it does, the same way Identity resolves Organization's own checker today.</summary>
internal sealed class FacultyMemberLookup(FacultyDbContext context) : IFacultyMemberLookup
{
    public async Task<FacultyMemberSummary?> GetAsync(Guid facultyMemberId, CancellationToken cancellationToken = default)
    {
        var facultyMember = await context.FacultyMembers
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == new Domain.FacultyMembers.FacultyMemberId(facultyMemberId), cancellationToken)
            .ConfigureAwait(false);

        return facultyMember is null
            ? null
            : new FacultyMemberSummary(facultyMember.Id.Value, facultyMember.DepartmentId, facultyMember.Status.ToString());
    }

    public async Task<FacultyMemberSummary?> GetByUserIdAsync(Guid identityUserId, CancellationToken cancellationToken = default)
    {
        var facultyMember = await context.FacultyMembers
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.UserId == identityUserId, cancellationToken)
            .ConfigureAwait(false);

        return facultyMember is null
            ? null
            : new FacultyMemberSummary(facultyMember.Id.Value, facultyMember.DepartmentId, facultyMember.Status.ToString());
    }
}
