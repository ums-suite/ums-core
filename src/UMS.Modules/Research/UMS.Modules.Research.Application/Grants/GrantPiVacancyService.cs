using System.Text.Json;
using UMS.Modules.Research.Application.Abstractions;
using UMS.Modules.Research.Application.Common;
using UMS.Modules.Research.Domain.Grants;
using UMS.Shared.Audit;

namespace UMS.Modules.Research.Application.Grants;

/// <summary>
/// RES-5: edge-cases.md "Principal Investigator Leaves the University Mid-Grant";
/// design-decisions.md "Grant Lifecycle State Machine and PI-Vacancy Handling" - called by the
/// <c>ResearchFacultyStatusRelayWorker</c> for every unprocessed <c>FacultyMemberStatusChanged</c>
/// envelope. Flags every <c>Funded</c>/<c>Active</c> Grant where the departed FacultyMember holds the
/// active PI role, publishing <see cref="Domain.Events.GrantPiReassignmentRequired"/> per Grant.
///
/// <para>
/// This module does NOT re-check the FacultyMember's new status here (e.g. only flag on
/// `Separated`/`Terminated`, not `OnLeave`) - <c>FacultyMemberStatusChanged</c>'s payload carries no
/// decoded status across the module boundary by design (mirrors Library's own envelope shape's own
/// remarks), so ANY status change for an active PI triggers the flag; a false-positive flag (e.g. a
/// temporary `OnLeave`) still only requires an Admin to look at it and decide, never blocks anything
/// irreversibly (the same "flag for human review" posture design-decisions.md already commits to).
/// </para>
/// </summary>
public sealed class GrantPiVacancyService(IGrantRepository grants, IUnitOfWork unitOfWork, IAuditRecorder auditRecorder, IClock clock)
{
    public async Task<int> ApplyFacultyStatusChangeAsync(Guid facultyMemberId, string correlationId, CancellationToken cancellationToken = default)
    {
        var affected = await grants.ListActiveWherePrincipalInvestigatorAsync(facultyMemberId, cancellationToken).ConfigureAwait(false);
        var flaggedCount = 0;

        foreach (var grant in affected)
        {
            if (grant.RequiresPiReassignment)
            {
                continue;
            }

            grant.FlagPiReassignmentRequired(facultyMemberId, clock.UtcNow);

            await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            var auditRequest = AuditContext.ForSystemJob(
                "research-faculty-status-relay",
                correlationId,
                "Grant",
                grant.Id.Value.ToString(),
                "flag_pi_reassignment_required",
                JsonSerializer.Serialize(new { requiresPiReassignment = false }),
                JsonSerializer.Serialize(new { requiresPiReassignment = true, vacatedFacultyMemberId = facultyMemberId }));

            var commitResult = await TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, auditRequest, cancellationToken).ConfigureAwait(false);
            if (commitResult.IsSuccess)
            {
                flaggedCount++;
            }
        }

        return flaggedCount;
    }
}
