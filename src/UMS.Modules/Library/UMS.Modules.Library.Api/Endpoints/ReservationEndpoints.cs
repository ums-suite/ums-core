using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Library.Application.Common;
using UMS.Modules.Library.Application.Reservations;
using UMS.Shared.Authorization;
using UMS.Shared.ErrorHandling;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Library.Api.Endpoints;

/// <summary>LIB-4: requirement-spec.md §6 Reservation rows.</summary>
internal static class ReservationEndpoints
{
    public static void MapReservationEndpoints(this RouteGroupBuilder group)
    {
        var reservations = group.MapGroup("/reservations");

        reservations.MapPost("/", async (CreateReservationRequestBody body, HttpContext httpContext, BorrowerContextService borrowerContext, ReservationService service, CancellationToken cancellationToken) =>
        {
            var borrower = await borrowerContext.ResolveOwnBorrowerAsync(httpContext.User.GetUserId(), cancellationToken).ConfigureAwait(false);
            if (borrower.IsFailure)
            {
                return borrower.Error!.ToProblemResult(httpContext);
            }

            var result = await service.CreateAsync(body.BookId, borrower.Value.BorrowerId, borrower.Value.BorrowerType, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequireLiveSession();

        reservations.MapGet("/me", async (HttpContext httpContext, BorrowerContextService borrowerContext, ReservationService service, CancellationToken cancellationToken) =>
        {
            var borrower = await borrowerContext.ResolveOwnBorrowerAsync(httpContext.User.GetUserId(), cancellationToken).ConfigureAwait(false);
            if (borrower.IsFailure)
            {
                return borrower.Error!.ToProblemResult(httpContext);
            }

            return Results.Ok(await service.GetByBorrowerAsync(borrower.Value.BorrowerId, cancellationToken).ConfigureAwait(false));
        }).RequireLiveSession();
    }
}

internal sealed record CreateReservationRequestBody(Guid BookId);
