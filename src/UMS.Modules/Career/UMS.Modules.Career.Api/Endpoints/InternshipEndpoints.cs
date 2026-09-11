using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Career.Application.Applications;
using UMS.Modules.Career.Application.Internships;
using UMS.Modules.Career.Application.Permissions;
using UMS.Shared.Authorization;
using UMS.Shared.ErrorHandling;

namespace UMS.Modules.Career.Api.Endpoints;

/// <summary>CAR-2/3/4/6/9: Internship posting lifecycle, browse, application submission, and withdrawal + cascade (requirement-spec.md §2.2, §2.4, §2.6, §6 `/career/internships`).</summary>
internal static class InternshipEndpoints
{
    public static void MapInternshipEndpoints(this RouteGroupBuilder group)
    {
        var internships = group.MapGroup("/internships");

        // CAR-4: browse/search - only Published/ApplicationsOpen unless the caller holds career.internship.manage AND explicitly asked for every status (staff review view).
        internships.MapGet("/", async (Guid? programId, Guid? employerProfileId, string? keyword, bool? includeAllStatuses, int? skip, int? take, HttpContext httpContext, InternshipService service, IPermissionResolver permissionResolver, CancellationToken cancellationToken) =>
        {
            var wantsAllStatuses = includeAllStatuses ?? false;
            var effectiveIncludeAllStatuses = wantsAllStatuses
                && await permissionResolver.CheckAsync(httpContext.User.GetUserId(), httpContext.User.GetSessionId(), CareerPermissions.InternshipManage, cancellationToken).ConfigureAwait(false) == PermissionCheckOutcome.Granted;

            return Results.Ok(await service.ListAsync(programId, employerProfileId, keyword, effectiveIncludeAllStatuses, skip ?? 0, take ?? 50, cancellationToken).ConfigureAwait(false));
        }).RequireLiveSession();

        internships.MapGet("/{id:guid}", async (Guid id, HttpContext httpContext, InternshipService service, CancellationToken cancellationToken) =>
        {
            var result = await service.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequireLiveSession();

        internships.MapPost("/", async (CreateInternshipRequest body, HttpContext httpContext, InternshipService service, CancellationToken cancellationToken) =>
        {
            var result = await service.CreateAsync(body, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(dto => Results.Created($"/api/v1/career/internships/{dto.Id}", dto), error => error.ToProblemResult(httpContext));
        }).RequirePermission(CareerPermissions.InternshipManage);

        internships.MapPatch("/{id:guid}", async (Guid id, EditInternshipRequest body, HttpContext httpContext, InternshipService service, CancellationToken cancellationToken) =>
        {
            var result = await service.EditAsync(id, body, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(CareerPermissions.InternshipManage);

        internships.MapPost("/{id:guid}/publish", async (Guid id, uint version, HttpContext httpContext, InternshipService service, CancellationToken cancellationToken) =>
        {
            var result = await service.PublishAsync(id, version, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(CareerPermissions.InternshipManage);

        internships.MapPost("/{id:guid}/open-applications", async (Guid id, uint version, HttpContext httpContext, InternshipService service, CancellationToken cancellationToken) =>
        {
            var result = await service.OpenApplicationsAsync(id, version, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(CareerPermissions.InternshipManage);

        internships.MapPost("/{id:guid}/close-applications", async (Guid id, uint version, HttpContext httpContext, InternshipService service, CancellationToken cancellationToken) =>
        {
            var result = await service.CloseApplicationsAsync(id, version, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(CareerPermissions.InternshipManage);

        // CAR-9: withdrawal itself commits first; the fan-out cascade to every non-terminal CareerApplication runs immediately after, in-process, never inside the posting's own transaction (design-decisions.md).
        internships.MapPost("/{id:guid}/withdraw", async (Guid id, WithdrawInternshipRequest body, HttpContext httpContext, InternshipService service, InternshipWithdrawalCascadeHandler cascadeHandler, CancellationToken cancellationToken) =>
        {
            var result = await service.WithdrawAsync(id, body, cancellationToken).ConfigureAwait(false);
            if (result.IsFailure)
            {
                return result.Error!.ToProblemResult(httpContext);
            }

            await cascadeHandler.HandleAsync(id, httpContext.GetCorrelationId(), cancellationToken).ConfigureAwait(false);
            return Results.Ok(result.Value);
        }).RequirePermission(CareerPermissions.InternshipManage);

        // CAR-6: the Student-facing application submission - write-time enforcement lives entirely in the guarded-insert repository method, never trusted from an earlier read here.
        internships.MapPost("/{id:guid}/apply", async (Guid id, ApplyToInternshipRequest body, HttpContext httpContext, InternshipApplicationService service, CancellationToken cancellationToken) =>
        {
            var result = await service.ApplyAsync(httpContext.User.GetUserId(), id, body, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(dto => Results.Created($"/api/v1/career/applications/{dto.Id}", dto), error => error.ToProblemResult(httpContext));
        }).RequireLiveSession();

        internships.MapGet("/{id:guid}/applications", async (Guid id, CareerApplicationReviewService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.ListByInternshipAsync(id, cancellationToken).ConfigureAwait(false))).RequirePermission(CareerPermissions.InternshipManage);
    }
}
