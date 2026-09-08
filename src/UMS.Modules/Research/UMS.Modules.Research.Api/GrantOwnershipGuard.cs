using Microsoft.AspNetCore.Http;
using UMS.Modules.Research.Application.Grants;
using UMS.Shared.Authorization;
using UMS.Shared.ErrorHandling;
using UMS.Shared.ErrorHandling.Results;
using UMS.Shared.Faculty;

namespace UMS.Modules.Research.Api;

/// <summary>
/// requirement-spec.md §6: "`fund`/`report`/`reject` reserved to Research-Office/Admin,
/// `activate`/`close` also available to the PI" - mirrors Library's own <c>OwnershipGuard</c>
/// dual-path pattern exactly, resolving "the PI" via <see cref="IFacultyMemberLookup.GetByUserIdAsync"/>
/// against the caller's own Identity user id rather than trusting a client-supplied FacultyMemberId.
/// </summary>
internal static class GrantOwnershipGuard
{
    public static async Task<IResult?> EnsureOwningPiOrPermissionAsync(
        GrantDto grant,
        HttpContext httpContext,
        IFacultyMemberLookup facultyMemberLookup,
        IPermissionResolver permissionResolver,
        string staffPermission,
        CancellationToken cancellationToken)
    {
        var caller = await facultyMemberLookup.GetByUserIdAsync(httpContext.User.GetUserId(), cancellationToken).ConfigureAwait(false);
        if (caller is not null && grant.PrincipalInvestigatorFacultyMemberId == caller.Id)
        {
            return null;
        }

        var outcome = await permissionResolver.CheckAsync(httpContext.User.GetUserId(), httpContext.User.GetSessionId(), staffPermission, cancellationToken).ConfigureAwait(false);
        return outcome == PermissionCheckOutcome.Granted
            ? null
            : Error.Forbidden("grant.not_owner", "You are not this Grant's Principal Investigator and do not hold the required staff permission.").ToProblemResult(httpContext);
    }
}
