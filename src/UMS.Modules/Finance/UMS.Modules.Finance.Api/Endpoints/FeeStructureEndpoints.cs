using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Finance.Application.FeeStructures;
using UMS.Modules.Finance.Application.Permissions;
using UMS.Shared.Authorization;
using UMS.Shared.ErrorHandling;

namespace UMS.Modules.Finance.Api.Endpoints;

/// <summary>FIN-1: requirement-spec.md §6's FeeStructure rows - Admin/Accountant managed CRUD, read-only to calling modules (§2).</summary>
internal static class FeeStructureEndpoints
{
    public static void MapFeeStructureEndpoints(this RouteGroupBuilder group)
    {
        var feeStructures = group.MapGroup("/fee-structures");

        feeStructures.MapPost("/", async (CreateFeeStructureRequest body, HttpContext httpContext, FeeStructureService service, CancellationToken cancellationToken) =>
        {
            var result = await service.CreateAsync(body, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(
                dto => Results.Created($"/api/v1/finance/fee-structures/{dto.Id}", dto),
                error => error.ToProblemResult(httpContext));
        }).RequirePermission(FinancePermissions.FeeStructureManage);

        feeStructures.MapPost("/{id:guid}/new-version", async (Guid id, PublishNewFeeStructureVersionRequest body, HttpContext httpContext, FeeStructureService service, CancellationToken cancellationToken) =>
        {
            var result = await service.PublishNewVersionAsync(id, body, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(FinancePermissions.FeeStructureManage);

        feeStructures.MapGet("/", async (HttpContext httpContext, FeeStructureService service, CancellationToken cancellationToken) =>
        {
            var result = await service.ListAsync(cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(FinancePermissions.FeeStructureManage);
    }
}
