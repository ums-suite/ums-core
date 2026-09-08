using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Hostel.Application.Allocations;
using UMS.Modules.Hostel.Application.Common;
using UMS.Modules.Hostel.Application.Permissions;
using UMS.Modules.Hostel.Domain.Allocations;
using UMS.Shared.Authorization;
using UMS.Shared.ErrorHandling;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Hostel.Api.Endpoints;

/// <summary>HOS-11/12/13/17: requirement-spec.md §6 Allocation rows.</summary>
internal static class AllocationEndpoints
{
    public static void MapAllocationEndpoints(this RouteGroupBuilder group)
    {
        var allocations = group.MapGroup("/allocations");

        allocations.MapGet("/me", async (HttpContext httpContext, StudentContextService studentContext, AllocationService service, CancellationToken cancellationToken) =>
        {
            var studentId = await OwnershipGuard.ResolveOwnStudentIdAsync(studentContext, httpContext, cancellationToken).ConfigureAwait(false);
            if (studentId.IsFailure)
            {
                return studentId.Error!.ToProblemResult(httpContext);
            }

            return Results.Ok(await service.GetByStudentAsync(studentId.Value, cancellationToken).ConfigureAwait(false));
        }).RequireLiveSession();

        allocations.MapPost("/{id:guid}/check-in", async (Guid id, HttpContext httpContext, AllocationService service, CancellationToken cancellationToken) =>
        {
            var result = await service.CheckInAsync(id, httpContext.User.GetUserId(), httpContext.GetCorrelationId(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(HostelPermissions.AllocationCheckIn);

        // HOS-17: not named in requirement-spec.md §6's own table, but a cheap, natural surface for
        // an Officer to see the advisory review flags design-decisions.md's additive side-table
        // records - never gates/forces anything itself (read-only).
        allocations.MapGet("/{id:guid}/review-flags", async (Guid id, AllocationReviewFlagService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetByAllocationAsync(id, cancellationToken).ConfigureAwait(false))).RequirePermission(HostelPermissions.ApplicationReview);

        // requirement-spec.md §6: "Officer or self-service checkout request" - one endpoint, two
        // authorization paths. A Student checking out their own Allocation is always treated as a
        // Voluntary check-out (the only type a self-service request can name); an Officer holding
        // hostel.allocation.checkout may name any CheckOutType (end-of-session/disciplinary too).
        allocations.MapPost("/{id:guid}/check-out", async (Guid id, CheckOutRequest body, HttpContext httpContext, StudentContextService studentContext, AllocationService service, IPermissionResolver permissionResolver, CancellationToken cancellationToken) =>
        {
            var allocation = await service.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
            if (allocation.IsFailure)
            {
                return allocation.Error!.ToProblemResult(httpContext);
            }

            var ownStudentId = await studentContext.ResolveOwnStudentIdAsync(httpContext.User.GetUserId(), cancellationToken).ConfigureAwait(false);
            var isOwner = ownStudentId.IsSuccess && ownStudentId.Value == allocation.Value.StudentId;

            CheckOutType checkOutType;
            if (isOwner)
            {
                checkOutType = CheckOutType.Voluntary;
            }
            else
            {
                var sessionId = Guid.Parse(httpContext.User.FindFirst(UmsClaimTypes.SessionId)?.Value ?? throw new InvalidOperationException("Missing 'sid' claim."));
                var outcome = await permissionResolver.CheckAsync(httpContext.User.GetUserId(), sessionId, HostelPermissions.AllocationCheckOut, cancellationToken).ConfigureAwait(false);
                if (outcome != PermissionCheckOutcome.Granted)
                {
                    return Error.Forbidden("hostel.not_owner", "You do not own this Allocation and do not hold the officer check-out permission.").ToProblemResult(httpContext);
                }

                if (!Enum.TryParse(body.CheckOutType, ignoreCase: true, out checkOutType))
                {
                    return Error.Validation("allocation.invalid_checkout_type", $"'{body.CheckOutType}' is not a recognized check-out type.").ToProblemResult(httpContext);
                }
            }

            var result = await service.CheckOutAsync(id, checkOutType, httpContext.User.GetUserId(), httpContext.GetCorrelationId(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequireLiveSession();
    }
}
