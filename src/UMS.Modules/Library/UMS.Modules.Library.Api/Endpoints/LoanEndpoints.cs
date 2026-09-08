using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Library.Application.Common;
using UMS.Modules.Library.Application.Loans;
using UMS.Modules.Library.Application.Permissions;
using UMS.Modules.Library.Domain.Common;
using UMS.Shared.Authorization;
using UMS.Shared.ErrorHandling;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Library.Api.Endpoints;

/// <summary>LIB-5..8/LIB-16: requirement-spec.md §6 Loan rows.</summary>
internal static class LoanEndpoints
{
    public static void MapLoanEndpoints(this RouteGroupBuilder group)
    {
        var loans = group.MapGroup("/loans");

        loans.MapPost("/", async (IssueLoanRequest body, HttpContext httpContext, LoanService service, CancellationToken cancellationToken) =>
        {
            if (!Enum.TryParse<BorrowerType>(body.BorrowerType, ignoreCase: true, out var borrowerType))
            {
                return Error.Validation("loan.invalid_borrower_type", $"'{body.BorrowerType}' is not a recognized borrower type.").ToProblemResult(httpContext);
            }

            var result = await service.IssueAsync(body.BookCopyId, body.BorrowerId, borrowerType, httpContext.User.GetUserId(), httpContext.GetCorrelationId(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(LibraryPermissions.LoanIssue);

        loans.MapGet("/me", async (HttpContext httpContext, BorrowerContextService borrowerContext, LoanService service, CancellationToken cancellationToken) =>
        {
            var borrower = await borrowerContext.ResolveOwnBorrowerAsync(httpContext.User.GetUserId(), cancellationToken).ConfigureAwait(false);
            if (borrower.IsFailure)
            {
                return borrower.Error!.ToProblemResult(httpContext);
            }

            return Results.Ok(await service.GetByBorrowerAsync(borrower.Value.BorrowerId, cancellationToken).ConfigureAwait(false));
        }).RequireLiveSession();

        loans.MapGet("/{id:guid}", async (Guid id, HttpContext httpContext, LoanService service, CancellationToken cancellationToken) =>
        {
            var result = await service.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(LibraryPermissions.LoanManage);

        // requirement-spec.md §2 Renewal: "Borrower or librarian renews" - a dual-path endpoint
        // mirroring Hostel's own AllocationEndpoints check-out pattern exactly.
        loans.MapPost("/{id:guid}/renew", async (Guid id, HttpContext httpContext, LoanService loanService, BorrowerContextService borrowerContext, IPermissionResolver permissionResolver, CancellationToken cancellationToken) =>
        {
            var loan = await loanService.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
            if (loan.IsFailure)
            {
                return loan.Error!.ToProblemResult(httpContext);
            }

            var guard = await OwnershipGuard.EnsureOwnerOrPermissionAsync(loan.Value.BorrowerId, httpContext, borrowerContext, permissionResolver, LibraryPermissions.LoanManage, cancellationToken).ConfigureAwait(false);
            if (guard is not null)
            {
                return guard;
            }

            var result = await loanService.RenewAsync(id, httpContext.User.GetUserId(), httpContext.GetCorrelationId(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequireLiveSession();

        loans.MapPost("/{id:guid}/return", async (Guid id, HttpContext httpContext, LoanService service, CancellationToken cancellationToken) =>
        {
            var result = await service.ReturnAsync(id, httpContext.User.GetUserId(), httpContext.GetCorrelationId(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(LibraryPermissions.LoanManage);

        // LIB-16: advisory-only, read-only surface - never gates/forces anything (mirrors Hostel's
        // own "review-flags" endpoint precedent).
        loans.MapGet("/{id:guid}/review-flags", async (Guid id, LoanReviewFlagService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetByLoanAsync(id, cancellationToken).ConfigureAwait(false))).RequirePermission(LibraryPermissions.ReviewFlagView);
    }
}
