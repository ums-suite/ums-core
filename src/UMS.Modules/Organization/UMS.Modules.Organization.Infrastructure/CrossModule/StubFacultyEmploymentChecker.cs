using UMS.Modules.Organization.Application.Abstractions;

namespace UMS.Modules.Organization.Infrastructure.CrossModule;

/// <summary>
/// EXPLICIT SEAM, not a real check: Faculty (release/DEVELOPMENT_PLAN.md Flow #10) does not exist
/// yet, so there is no read interface to call. Every Department is reported as having no active
/// FacultyMember until Faculty lands and this registration is replaced with one that actually
/// calls Faculty's public query interface (edge-cases.md, "Deactivating a Department that still
/// has a Faculty member on record"). Deliberately kept as its own named type (rather than an
/// inline lambda) so it is easy to find and delete when that day comes - mirrors Identity's own
/// former <c>StubOrganizationNodeExistenceChecker</c> exactly.
/// </summary>
internal sealed class StubFacultyEmploymentChecker : IFacultyEmploymentChecker
{
    public Task<bool> HasActiveFacultyMemberAsync(Guid departmentId, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);
}
