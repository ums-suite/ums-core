using UMS.Modules.Identity.Application.Abstractions;
using UMS.Modules.Identity.Domain.Users;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Identity.Application.Users;

/// <summary>IDN-13: suspend/reactivate a User (requirement-spec.md identity §3 `UserStatusChanged`, §6 `PATCH /users/{id}/status`).</summary>
public sealed class UserStatusService(IUserRepository users, IUnitOfWork unitOfWork, IAuthzCache authzCache, IClock clock)
{
    public async Task<Result<UserDto>> ChangeStatusAsync(Guid userId, UserStatus targetStatus, CancellationToken cancellationToken = default)
    {
        var user = await users.GetByIdAsync(new UserId(userId), cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return Error.NotFound("user.not_found", $"No User exists with id '{userId}'.");
        }

        if (user.Status == targetStatus)
        {
            return Error.Conflict("user.status_unchanged", $"User is already {targetStatus}.");
        }

        var now = clock.UtcNow;
        if (targetStatus == UserStatus.Suspended)
        {
            user.Suspend(now);
        }
        else
        {
            user.Reactivate(now);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // A valid JWT is never sufficient by itself (identity §4) - a suspended User's live
        // permission checks must fail on their very next request, not merely after a cache's TTL
        // expiry (design-decisions.md, "Permission-Check Caching vs. Live Lookup").
        await authzCache.InvalidateUserAsync(user.Id, cancellationToken).ConfigureAwait(false);

        return UserProvisioningService.ToDto(user);
    }
}
