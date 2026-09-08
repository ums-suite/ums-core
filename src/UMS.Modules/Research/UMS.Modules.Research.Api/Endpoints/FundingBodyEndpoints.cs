using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Research.Application.FundingBodies;
using UMS.Modules.Research.Application.Permissions;
using UMS.Shared.Authorization;
using UMS.Shared.ErrorHandling;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Research.Api.Endpoints;

/// <summary>RES-1: requirement-spec.md §2 Funding Body Reference Data, §6 API Surface row 1.</summary>
internal static class FundingBodyEndpoints
{
    public static void MapFundingBodyEndpoints(this RouteGroupBuilder group)
    {
        var fundingBodies = group.MapGroup("/funding-bodies");

        fundingBodies.MapPost("/", async (CreateFundingBodyRequest body, HttpContext httpContext, FundingBodyService service, CancellationToken cancellationToken) =>
        {
            var result = await service.CreateAsync(body, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(
                dto => Results.Created($"/api/v1/research/funding-bodies/{dto.Id}", dto),
                error => error.ToProblemResult(httpContext));
        }).RequirePermission(ResearchPermissions.FundingBodyManage);

        fundingBodies.MapPut("/{id:guid}", async (Guid id, UpdateFundingBodyRequest body, HttpContext httpContext, FundingBodyService service, CancellationToken cancellationToken) =>
        {
            var result = await service.UpdateAsync(id, body, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(ResearchPermissions.FundingBodyManage);

        // Reference data read by every authenticated Faculty/Admin caller proposing or reviewing a
        // Grant - not part of the anonymous public showcase (§6), so RequireLiveSession rather than
        // AllowAnonymous.
        fundingBodies.MapGet("/{id:guid}", async (Guid id, HttpContext httpContext, FundingBodyService service, CancellationToken cancellationToken) =>
        {
            var result = await service.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequireLiveSession();

        fundingBodies.MapGet("/", async (int? skip, int? take, FundingBodyService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.ListAsync(skip ?? 0, take ?? 50, cancellationToken).ConfigureAwait(false)))
            .RequireLiveSession();
    }
}
