using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Career.Application.Employers;
using UMS.Modules.Career.Application.Permissions;
using UMS.Shared.Authorization;
using UMS.Shared.ErrorHandling;

namespace UMS.Modules.Career.Api.Endpoints;

/// <summary>CAR-1: staff-curated `EmployerProfile` CRUD (requirement-spec.md §2.1, §6 `/career/employers`) - no employer-facing login exists, so every write is gated by `career.employer.manage`.</summary>
internal static class EmployerEndpoints
{
    public static void MapEmployerEndpoints(this RouteGroupBuilder group)
    {
        var employers = group.MapGroup("/employers");

        employers.MapGet("/", async (bool? includeArchived, int? skip, int? take, EmployerProfileService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.ListAsync(includeArchived ?? false, skip ?? 0, take ?? 50, cancellationToken).ConfigureAwait(false))).RequireLiveSession();

        employers.MapGet("/{id:guid}", async (Guid id, HttpContext httpContext, EmployerProfileService service, CancellationToken cancellationToken) =>
        {
            var result = await service.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequireLiveSession();

        employers.MapPost("/", async (CreateEmployerProfileRequest body, HttpContext httpContext, EmployerProfileService service, CancellationToken cancellationToken) =>
        {
            var result = await service.CreateAsync(body, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(dto => Results.Created($"/api/v1/career/employers/{dto.Id}", dto), error => error.ToProblemResult(httpContext));
        }).RequirePermission(CareerPermissions.EmployerManage);

        employers.MapPatch("/{id:guid}", async (Guid id, UpdateEmployerProfileRequest body, HttpContext httpContext, EmployerProfileService service, CancellationToken cancellationToken) =>
        {
            var result = await service.UpdateAsync(id, body, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(CareerPermissions.EmployerManage);

        employers.MapPost("/{id:guid}/archive", async (Guid id, uint version, HttpContext httpContext, EmployerProfileService service, CancellationToken cancellationToken) =>
        {
            var result = await service.ArchiveAsync(id, version, archive: true, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(CareerPermissions.EmployerManage);

        employers.MapPost("/{id:guid}/unarchive", async (Guid id, uint version, HttpContext httpContext, EmployerProfileService service, CancellationToken cancellationToken) =>
        {
            var result = await service.ArchiveAsync(id, version, archive: false, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(CareerPermissions.EmployerManage);
    }
}
