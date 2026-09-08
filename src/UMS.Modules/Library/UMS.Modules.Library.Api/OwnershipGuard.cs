using Microsoft.AspNetCore.Http;
using UMS.Modules.Library.Application.Common;
using UMS.Modules.Library.Application.Permissions;
using UMS.Shared.Authorization;
using UMS.Shared.ErrorHandling;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Library.Api;

/// <summary>
/// requirement-spec.md §6: "Every endpoint enforces resource ownership and organizational scope" -
/// several Library endpoints (renew, return, settle) are dual-path: the resource's own owning
/// borrower may act on it directly, OR a librarian holding the relevant staff permission may act on
/// anyone's behalf - mirrors Hostel's own <c>AllocationEndpoints</c> check-out dual-path exactly.
/// </summary>
internal static class OwnershipGuard
{
    public static async Task<IResult?> EnsureOwnerOrPermissionAsync(
        Guid resourceBorrowerId,
        HttpContext httpContext,
        BorrowerContextService borrowerContext,
        IPermissionResolver permissionResolver,
        string staffPermission,
        CancellationToken cancellationToken)
    {
        var own = await borrowerContext.ResolveOwnBorrowerAsync(httpContext.User.GetUserId(), cancellationToken).ConfigureAwait(false);
        if (own.IsSuccess && own.Value.BorrowerId == resourceBorrowerId)
        {
            return null;
        }

        var outcome = await permissionResolver.CheckAsync(httpContext.User.GetUserId(), httpContext.User.GetSessionId(), staffPermission, cancellationToken).ConfigureAwait(false);
        return outcome == PermissionCheckOutcome.Granted
            ? null
            : Error.Forbidden("library.not_owner", "You do not own this resource and do not hold the required staff permission.").ToProblemResult(httpContext);
    }
}
