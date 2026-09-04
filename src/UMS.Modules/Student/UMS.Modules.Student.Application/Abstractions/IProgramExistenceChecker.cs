namespace UMS.Modules.Student.Application.Abstractions;

/// <summary>
/// Student's own local port for validating a Program reference at <c>CreateStudentRecord</c> time
/// (docs/ddd/ubiquitous-language.md: <c>Program</c> is owned by <c>Academic</c>, glossary "Owned by
/// Academic"). Academic (release/DEVELOPMENT_PLAN.md Flow #12) does not exist yet in this build, so
/// Student.Infrastructure registers a permissive stub implementation
/// (<c>StubProgramExistenceChecker</c>) against this interface, exactly the same "accepted as
/// existing until the owning module lands" arc <c>UMS.Modules.Organization.Application.Abstractions.
/// IFacultyEmploymentChecker</c>'s own former stub went through before Faculty (Flow #10) existed to
/// implement it for real - promote this to a real check once Academic exists (Flow #12/16).
/// </summary>
public interface IProgramExistenceChecker
{
    public Task<bool> ExistsAsync(Guid programId, CancellationToken cancellationToken = default);
}
