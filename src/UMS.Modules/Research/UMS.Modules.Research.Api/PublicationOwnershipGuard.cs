using Microsoft.AspNetCore.Http;
using UMS.Modules.Research.Application.Publications;
using UMS.Shared.Authorization;
using UMS.Shared.ErrorHandling;
using UMS.Shared.ErrorHandling.Results;
using UMS.Shared.Faculty;

namespace UMS.Modules.Research.Api;

/// <summary>requirement-spec.md §6: "Any listed internal author or Admin" - create/update ownership check, mirroring <see cref="GrantOwnershipGuard"/>'s own shape.</summary>
internal static class PublicationOwnershipGuard
{
    public static async Task<IResult?> EnsureAuthorOrPermissionAsync(
        IEnumerable<AuthorEntryDto> authors,
        HttpContext httpContext,
        IFacultyMemberLookup facultyMemberLookup,
        IPermissionResolver permissionResolver,
        string staffPermission,
        CancellationToken cancellationToken)
    {
        var caller = await facultyMemberLookup.GetByUserIdAsync(httpContext.User.GetUserId(), cancellationToken).ConfigureAwait(false);
        if (caller is not null && authors.Any(a => a.FacultyMemberId == caller.Id))
        {
            return null;
        }

        var outcome = await permissionResolver.CheckAsync(httpContext.User.GetUserId(), httpContext.User.GetSessionId(), staffPermission, cancellationToken).ConfigureAwait(false);
        return outcome == PermissionCheckOutcome.Granted
            ? null
            : Error.Forbidden("publication.not_author", "You are not a listed internal author on this Publication and do not hold the required staff permission.").ToProblemResult(httpContext);
    }
}
