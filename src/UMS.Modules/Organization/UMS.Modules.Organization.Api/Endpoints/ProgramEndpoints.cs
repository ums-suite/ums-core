using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Organization.Api.Contracts;
using UMS.Modules.Organization.Application.Permissions;
using UMS.Modules.Organization.Application.Programs;
using UMS.Shared.Authorization;
using UMS.Shared.ErrorHandling;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Organization.Api.Endpoints;

/// <summary>ORG-5: Program record CRUD + deactivate (requirement-spec.md organization §6/§9.1) - Academic is notified via the `ProgramCreated` outbox event, not synchronously here.</summary>
internal static class ProgramEndpoints
{
    public static void MapProgramEndpoints(this RouteGroupBuilder group)
    {
        var programs = group.MapGroup("/programs");

        programs.MapGet("/", async (Guid? departmentId, int? skip, int? take, string? lang, ProgramService service, CancellationToken cancellationToken) =>
        {
            var page = await service.ListAsync(departmentId, skip ?? 0, take ?? 50, lang, cancellationToken).ConfigureAwait(false);
            return Results.Ok(page);
        }).AllowAnonymous();

        programs.MapGet("/{id:guid}", async (Guid id, string? lang, HttpContext httpContext, ProgramService service, CancellationToken cancellationToken) =>
        {
            var result = await service.GetByIdAsync(id, lang, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).AllowAnonymous();

        programs.MapPost("/", async (CreateProgramRequest body, string? lang, HttpContext httpContext, ProgramService service, CancellationToken cancellationToken) =>
        {
            var result = await service.CreateAsync(body, httpContext.GetAuditContext(), lang, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(
                dto => Results.Created($"/api/v1/organization/programs/{dto.Id}", dto),
                error => error.ToProblemResult(httpContext));
        }).RequirePermission(OrganizationPermissions.ProgramManage);

        programs.MapPatch("/{id:guid}", async (Guid id, UpdateProgramRequest body, string? lang, HttpContext httpContext, ProgramService service, CancellationToken cancellationToken) =>
        {
            var result = await service.UpdateAsync(id, body, httpContext.GetAuditContext(), lang, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(OrganizationPermissions.ProgramManage);

        programs.MapPost("/{id:guid}/deactivate", async (Guid id, DeactivateRequestBody body, HttpContext httpContext, ProgramService service, CancellationToken cancellationToken) =>
        {
            var result = await service.DeactivateAsync(id, body.Version, httpContext.GetAuditContext(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(OrganizationPermissions.ProgramManage);
    }
}
