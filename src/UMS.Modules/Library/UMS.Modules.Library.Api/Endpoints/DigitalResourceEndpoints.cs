using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Library.Application.Catalog;
using UMS.Modules.Library.Application.Common;
using UMS.Shared.Authorization;
using UMS.Shared.ErrorHandling;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Library.Api.Endpoints;

/// <summary>LIB-15: requirement-spec.md §6 <c>GET /digital-resources/{id}/access</c>.</summary>
internal static class DigitalResourceEndpoints
{
    public static void MapDigitalResourceEndpoints(this RouteGroupBuilder group)
    {
        group.MapGroup("/digital-resources").MapGet("/{id:guid}/access", async (Guid id, HttpContext httpContext, BorrowerContextService borrowerContext, DigitalResourceAccessService service, CancellationToken cancellationToken) =>
        {
            var borrower = await borrowerContext.ResolveOwnBorrowerAsync(httpContext.User.GetUserId(), cancellationToken).ConfigureAwait(false);
            if (borrower.IsFailure)
            {
                return borrower.Error!.ToProblemResult(httpContext);
            }

            var result = await service.RequestAccessAsync(id, borrower.Value.BorrowerId, borrower.Value.BorrowerType, httpContext.GetCorrelationId(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequireLiveSession();
    }
}
