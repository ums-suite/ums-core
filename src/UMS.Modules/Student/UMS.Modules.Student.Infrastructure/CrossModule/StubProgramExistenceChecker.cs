using UMS.Modules.Student.Application.Abstractions;

namespace UMS.Modules.Student.Infrastructure.CrossModule;

/// <summary>
/// Permissive first-pass stub for <see cref="IProgramExistenceChecker"/> - Academic (release/
/// DEVELOPMENT_PLAN.md Flow #12) does not exist yet, so there is no real Program registry to check
/// against. Mirrors the exact "accepted as existing until the owning module lands" arc
/// <c>UMS.Modules.Organization.Application.Abstractions.IFacultyEmploymentChecker</c>'s own former
/// <c>StubFacultyEmploymentChecker</c> went through before Faculty existed. A non-empty
/// <paramref name="programId"/> is accepted unconditionally; promote this to a real check delegating
/// to Academic's own future <c>UMS.Shared.Academic</c>-style shared interface once Flow #12/16
/// lands.
/// </summary>
internal sealed class StubProgramExistenceChecker : IProgramExistenceChecker
{
    public Task<bool> ExistsAsync(Guid programId, CancellationToken cancellationToken = default) =>
        Task.FromResult(programId != Guid.Empty);
}
