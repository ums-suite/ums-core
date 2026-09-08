namespace UMS.Shared.Organization;

/// <summary>
/// CAR-10: the read-only lookup Career (release/DEVELOPMENT_PLAN.md Flow #30; requirement-spec.md
/// career §2.3/§7 - "a CampusRecruitmentDrive's venue... resolves against Organization's own `Room`
/// (the same entity Academic uses for exam rooms)") needs to validate a Drive's venue reference
/// without a competing Career-owned venue concept.
///
/// <para>
/// Living in <c>UMS.Shared.Organization</c> - not <c>UMS.Modules.Organization.*</c> - is what lets
/// Career call it without a forbidden dependency on Organization's Domain/Application/Infrastructure
/// internals (module-boundaries.md, ADR-0002), mirroring <see cref="IOrganizationNodeExistenceChecker"/>'s
/// own exact pattern - Career is this contract's first real caller (Organization's own Infrastructure
/// layer registers the one real implementation directly, the same "first mover, no stub-then-promote
/// dance needed" shape <c>UMS.Shared.Student.IStudentStatusChecker</c>'s own remarks already document
/// for Academic).
/// </para>
/// </summary>
public interface IRoomExistenceChecker
{
    /// <summary>True if <paramref name="roomId"/> is the id of an existing `Room` row. Existence alone is checked, not active status - a since-deactivated room may still be a legitimate historical venue reference.</summary>
    public Task<bool> ExistsAsync(Guid roomId, CancellationToken cancellationToken = default);
}
