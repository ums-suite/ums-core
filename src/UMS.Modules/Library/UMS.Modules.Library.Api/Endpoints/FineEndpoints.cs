using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Library.Application.Common;
using UMS.Modules.Library.Application.Fines;
using UMS.Modules.Library.Application.Permissions;
using UMS.Shared.Authorization;
using UMS.Shared.ErrorHandling;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Library.Api.Endpoints;

/// <summary>LIB-12..14: requirement-spec.md §6 Fine rows.</summary>
internal static class FineEndpoints
{
    public static void MapFineEndpoints(this RouteGroupBuilder group)
    {
        var fines = group.MapGroup("/fines");

        fines.MapGet("/me", async (HttpContext httpContext, BorrowerContextService borrowerContext, FineService service, CancellationToken cancellationToken) =>
        {
            var borrower = await borrowerContext.ResolveOwnBorrowerAsync(httpContext.User.GetUserId(), cancellationToken).ConfigureAwait(false);
            if (borrower.IsFailure)
            {
                return borrower.Error!.ToProblemResult(httpContext);
            }

            return Results.Ok(await service.GetByBorrowerAsync(borrower.Value.BorrowerId, cancellationToken).ConfigureAwait(false));
        }).RequireLiveSession();

        // requirement-spec.md §2 Fine Accrual Settlement: a borrower-initiated payment request - a
        // librarian may also initiate settlement on a borrower's behalf, the same dual-path shape
        // as Loan renewal (see OwnershipGuard).
        fines.MapPost("/{id:guid}/settle", async (Guid id, HttpContext httpContext, FineService fineService, BorrowerContextService borrowerContext, IPermissionResolver permissionResolver, CancellationToken cancellationToken) =>
        {
            var fine = await fineService.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
            if (fine.IsFailure)
            {
                return fine.Error!.ToProblemResult(httpContext);
            }

            var guard = await OwnershipGuard.EnsureOwnerOrPermissionAsync(fine.Value.BorrowerId, httpContext, borrowerContext, permissionResolver, LibraryPermissions.LoanManage, cancellationToken).ConfigureAwait(false);
            if (guard is not null)
            {
                return guard;
            }

            var settled = await fineService.SettleAsync(id, httpContext.User.GetUserId(), httpContext.GetCorrelationId(), cancellationToken).ConfigureAwait(false);
            return settled.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequireLiveSession();

        fines.MapPost("/{id:guid}/waive", async (Guid id, WaiveFineRequest body, HttpContext httpContext, FineService service, CancellationToken cancellationToken) =>
        {
            var result = await service.WaiveAsync(id, httpContext.User.GetUserId(), body.Reason, httpContext.GetCorrelationId(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(LibraryPermissions.FineWaive);
    }
}
