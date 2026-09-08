using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Alumni.Application.Common;
using UMS.Modules.Alumni.Application.Jobs;
using UMS.Modules.Alumni.Application.Permissions;
using UMS.Shared.Authorization;
using UMS.Shared.ErrorHandling;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Alumni.Api.Endpoints;

/// <summary>ALM-5/ALM-6/ALM-7: requirement-spec.md §6 <c>/alumni/jobs</c> rows.</summary>
internal static class JobEndpoints
{
    public static void MapJobEndpoints(this RouteGroupBuilder group)
    {
        var jobs = group.MapGroup("/jobs");

        jobs.MapGet("/", async (string? status, Guid? posterUserId, int? skip, int? take, JobPostingService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.ListAsync(status, posterUserId, skip ?? 0, take ?? 50, cancellationToken).ConfigureAwait(false))).RequireLiveSession();

        jobs.MapGet("/{id:guid}", async (Guid id, HttpContext httpContext, JobPostingService service, CancellationToken cancellationToken) =>
        {
            var result = await service.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequireLiveSession();

        // requirement-spec.md §2.3/§9: alumnus-posted jobs auto-publish; non-alumnus employer postings are pre-moderated.
        jobs.MapPost("/", async (PostJobRequest body, HttpContext httpContext, JobPostingService service, CallerAlumnusResolver resolver, CancellationToken cancellationToken) =>
        {
            var alumnus = await resolver.ResolveAsync(httpContext.User.GetUserId(), cancellationToken).ConfigureAwait(false);
            var result = await service.PostAsync(httpContext.User.GetUserId(), alumnus is not null, alumnus?.Id.Value, body, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(dto => Results.Created($"/api/v1/alumni/jobs/{dto.Id}", dto), error => error.ToProblemResult(httpContext));
        }).RequireLiveSession();

        // The poster's own edit - ownership enforced here (design-decisions.md's optimistic-concurrency mechanism handles the RACE; this check handles WHO may attempt the write at all).
        jobs.MapPatch("/{id:guid}", async (Guid id, EditJobPostingRequest body, HttpContext httpContext, JobPostingService service, CancellationToken cancellationToken) =>
        {
            var existing = await service.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
            if (existing.IsFailure)
            {
                return existing.Error!.ToProblemResult(httpContext);
            }

            if (existing.Value.PosterUserId != httpContext.User.GetUserId())
            {
                return Error.Forbidden("jobposting.not_owner", "Only the original poster may edit this JobPosting.").ToProblemResult(httpContext);
            }

            var result = await service.EditAsync(id, body, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequireLiveSession();

        // Poster withdraws their own posting; Admin can remove ANY posting at any time (requirement-spec.md §2.3 last bullet).
        jobs.MapDelete("/{id:guid}", async (Guid id, [FromBody] RemoveJobPostingRequest body, HttpContext httpContext, JobPostingService service, UMS.Shared.Authorization.IPermissionResolver permissionResolver, CancellationToken cancellationToken) =>
        {
            var existing = await service.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
            if (existing.IsFailure)
            {
                return existing.Error!.ToProblemResult(httpContext);
            }

            var isAdmin = await permissionResolver.CheckAsync(httpContext.User.GetUserId(), httpContext.User.GetSessionId(), AlumniPermissions.JobModerate, cancellationToken).ConfigureAwait(false) == UMS.Shared.Authorization.PermissionCheckOutcome.Granted;
            if (isAdmin)
            {
                var removed = await service.RemoveAsync(id, body, httpContext.GetAuditContext(), cancellationToken).ConfigureAwait(false);
                return removed.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
            }

            if (existing.Value.PosterUserId != httpContext.User.GetUserId())
            {
                return Error.Forbidden("jobposting.not_owner", "Only the original poster or an Admin may withdraw/remove this JobPosting.").ToProblemResult(httpContext);
            }

            var withdrawn = await service.WithdrawAsync(id, body.Version, cancellationToken).ConfigureAwait(false);
            return withdrawn.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequireLiveSession();

        jobs.MapPost("/{id:guid}/apply", async (Guid id, ApplyToJobRequest body, HttpContext httpContext, JobApplicationService service, CallerAlumnusResolver resolver, CancellationToken cancellationToken) =>
        {
            var alumnus = await resolver.ResolveAsync(httpContext.User.GetUserId(), cancellationToken).ConfigureAwait(false);
            var result = await service.ApplyAsync(id, httpContext.User.GetUserId(), alumnus is not null, body, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(dto => Results.Created($"/api/v1/alumni/jobs/{id}/apply/{dto.Id}", dto), error => error.ToProblemResult(httpContext));
        }).RequireLiveSession();

        jobs.MapGet("/{id:guid}/applications", async (Guid id, JobApplicationService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.ListByPostingAsync(id, cancellationToken).ConfigureAwait(false))).RequirePermission(AlumniPermissions.JobModerate);

        // requirement-spec.md §5/§7: moderation actions are audited.
        jobs.MapPost("/{id:guid}/moderate", async (Guid id, ModerateJobPostingRequest body, HttpContext httpContext, JobPostingService service, CancellationToken cancellationToken) =>
        {
            var result = await service.ModerateAsync(id, body, httpContext.GetAuditContext(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(AlumniPermissions.JobModerate);
    }
}
