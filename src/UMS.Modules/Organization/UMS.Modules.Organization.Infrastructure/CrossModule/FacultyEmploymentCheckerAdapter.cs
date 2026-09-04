using UMS.Modules.Organization.Application.Abstractions;

namespace UMS.Modules.Organization.Infrastructure.CrossModule;

/// <summary>
/// The REAL implementation, replacing <c>StubFacultyEmploymentChecker</c> now that Faculty
/// (release/DEVELOPMENT_PLAN.md Flow #10) exists - delegates to
/// <c>UMS.Shared.Faculty.IFacultyEmploymentChecker</c>, Faculty's own promoted shared contract,
/// mirroring exactly how Identity's former <c>StubOrganizationNodeExistenceChecker</c> was
/// replaced by a real adapter once Organization (Flow #6) landed.
/// </summary>
internal sealed class FacultyEmploymentCheckerAdapter(UMS.Shared.Faculty.IFacultyEmploymentChecker facultyChecker) : IFacultyEmploymentChecker
{
    public Task<bool> HasActiveFacultyMemberAsync(Guid departmentId, CancellationToken cancellationToken = default) =>
        facultyChecker.HasActiveFacultyMemberAsync(departmentId, cancellationToken);
}
