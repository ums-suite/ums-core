using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Hostel.Application.Applications;
using UMS.Modules.Hostel.Application.ApplicationWindows;
using UMS.Modules.Hostel.Application.Permissions;
using UMS.Shared.Authorization;
using UMS.Shared.ErrorHandling;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Hostel.Api.Endpoints;

/// <summary>HOS-2: requirement-spec.md §2 step 1, step 3; §9 decision 4. HOS-4's ranking pass is exposed here too, officer-triggered (no automatic scheduling was named by any ticket).</summary>
internal static class ApplicationWindowEndpoints
{
    public static void MapApplicationWindowEndpoints(this RouteGroupBuilder group)
    {
        var windows = group.MapGroup("/application-windows");

        windows.MapGet("/", async (ApplicationWindowService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetAllAsync(cancellationToken).ConfigureAwait(false))).RequireLiveSession();

        windows.MapGet("/{id:guid}", async (Guid id, HttpContext httpContext, ApplicationWindowService service, CancellationToken cancellationToken) =>
        {
            var result = await service.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequireLiveSession();

        windows.MapPost("/", async (CreateApplicationWindowRequest body, HttpContext httpContext, ApplicationWindowService service, CancellationToken cancellationToken) =>
        {
            var result = await service.CreateAsync(body, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(HostelPermissions.ApplicationWindowManage);

        windows.MapPut("/{id:guid}/eligible-programs", async (Guid id, ReplaceEligibleProgramsRequest body, HttpContext httpContext, ApplicationWindowService service, CancellationToken cancellationToken) =>
        {
            var result = await service.ReplaceEligibleProgramsAsync(id, body, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(HostelPermissions.ApplicationWindowManage);

        windows.MapPut("/{id:guid}/eligible-years", async (Guid id, ReplaceEligibleYearsRequest body, HttpContext httpContext, ApplicationWindowService service, CancellationToken cancellationToken) =>
        {
            var result = await service.ReplaceEligibleYearsAsync(id, body, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(HostelPermissions.ApplicationWindowManage);

        windows.MapPut("/{id:guid}/eligibility-rules", async (Guid id, ReplaceEligibilityRulesRequest body, HttpContext httpContext, ApplicationWindowService service, CancellationToken cancellationToken) =>
        {
            var result = await service.ReplaceEligibilityRulesAsync(id, body, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(HostelPermissions.ApplicationWindowManage);

        windows.MapPost("/{id:guid}/rank", async (Guid id, HttpContext httpContext, HostelApplicationRankingService service, CancellationToken cancellationToken) =>
        {
            var result = await service.RankWindowAsync(id, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(count => Results.Ok(new { rankedCount = count }), error => error.ToProblemResult(httpContext));
        }).RequirePermission(HostelPermissions.ApplicationReview);
    }
}
