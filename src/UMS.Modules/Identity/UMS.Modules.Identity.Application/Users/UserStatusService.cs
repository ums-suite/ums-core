using System.Text.Json;
using UMS.Modules.Identity.Application.Abstractions;
using UMS.Modules.Identity.Domain.Users;
using UMS.Shared.Audit;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Identity.Application.Users;

/// <summary>
/// IDN-13: suspend/reactivate a User (requirement-spec.md identity §3 `UserStatusChanged`, §6
/// `PATCH /users/{id}/status`). Also this module's minimal AUD-1 integration vehicle
/// (release/DEVELOPMENT_PLAN.md Flow #5's own call-site guidance): requirement-spec.md audit §7
/// names "user status change (suspend/reactivate)" as one of Identity's mutations that must call
/// <c>RecordEntry</c>, so this one call site demonstrates and integration-tests the real
/// cross-module, same-transaction write path end to end. The other mutations audit §7 lists for
/// Identity (role assignment/revocation, login failure, session revocation, password change) are
/// intentionally left unwired - Audit's own tickets.md explicitly scopes wiring every mutation to
/// each calling module's own backlog, not Flow #5's.
/// </summary>
public sealed class UserStatusService(IUserRepository users, IUnitOfWork unitOfWork, IAuthzCache authzCache, IAuditRecorder auditRecorder, IClock clock)
{
    public async Task<Result<UserDto>> ChangeStatusAsync(
        Guid userId,
        UserStatus targetStatus,
        Guid actorUserId,
        string? actorIpAddress,
        string correlationId,
        CancellationToken cancellationToken = default)
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
        var previousStatus = user.Status;

        // Opens Identity's own explicit transaction so the status mutation below and the Audit
        // entry commit or roll back together (ADR-0012) - see IUnitOfWork.BeginTransactionAsync's
        // remarks for why an explicit transaction, rather than SaveChangesAsync's own implicit
        // one, is required to hand a shareable DbTransaction to IAuditRecorder.
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        if (targetStatus == UserStatus.Suspended)
        {
            user.Suspend(now);
        }
        else
        {
            user.Reactivate(now);
        }

        // TODO: "which of the six SPAs originated this request" (the Application field below)
        // needs a platform-wide calling-application header convention (none exists yet) - out of
        // scope for this one call site to invent; hardcoded until that convention lands.
        var auditRequest = new RecordAuditEntryRequest(
            ActorId: actorUserId.ToString(),
            ActorType: AuditActorType.User,
            IpAddress: actorIpAddress,
            Application: "ums-admin-web",
            EntityType: "User",
            EntityId: userId.ToString(),
            Action: targetStatus == UserStatus.Suspended ? "suspend" : "reactivate",
            BeforeValueJson: JsonSerializer.Serialize(new { status = previousStatus.ToString() }),
            AfterValueJson: JsonSerializer.Serialize(new { status = targetStatus.ToString() }),
            CorrelationId: correlationId);

        var auditResult = await auditRecorder.RecordEntryAsync(auditRequest, transaction.DbTransaction, cancellationToken).ConfigureAwait(false);
        if (auditResult.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return auditResult.Error!;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        // A valid JWT is never sufficient by itself (identity §4) - a suspended User's live
        // permission checks must fail on their very next request, not merely after a cache's TTL
        // expiry (design-decisions.md, "Permission-Check Caching vs. Live Lookup").
        await authzCache.InvalidateUserAsync(user.Id, cancellationToken).ConfigureAwait(false);

        return UserProvisioningService.ToDto(user);
    }
}
