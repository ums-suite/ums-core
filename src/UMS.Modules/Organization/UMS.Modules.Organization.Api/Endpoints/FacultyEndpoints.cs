using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Organization.Api.Contracts;
using UMS.Modules.Organization.Application.Faculties;
using UMS.Modules.Organization.Application.Permissions;
using UMS.Shared.Authorization;
using UMS.Shared.ErrorHandling;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Organization.Api.Endpoints;

/// <summary>ORG-3: Faculty CRUD + deactivate (requirement-spec.md organization §6). `?lang=` resolves ORG-8's translated name server-side (ADR-0011).</summary>
internal static class FacultyEndpoints
{
    public static void MapFacultyEndpoints(this RouteGroupBuilder group)
    {
        var faculties = group.MapGroup("/faculties");

        faculties.MapGet("/", async (Guid? campusId, int? skip, int? take, string? lang, FacultyService service, CancellationToken cancellationToken) =>
        {
            var page = await service.ListAsync(campusId, skip ?? 0, take ?? 50, lang, cancellationToken).ConfigureAwait(false);
            return Results.Ok(page);
        }).AllowAnonymous();

        faculties.MapGet("/{id:guid}", async (Guid id, string? lang, HttpContext httpContext, FacultyService service, CancellationToken cancellationToken) =>
        {
            var result = await service.GetByIdAsync(id, lang, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).AllowAnonymous();

        faculties.MapPost("/", async (CreateFacultyRequest body, string? lang, HttpContext httpContext, FacultyService service, CancellationToken cancellationToken) =>
        {
            var result = await service.CreateAsync(body, httpContext.GetAuditContext(), lang, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(
                dto => Results.Created($"/api/v1/organization/faculties/{dto.Id}", dto),
                error => error.ToProblemResult(httpContext));
        }).RequirePermission(OrganizationPermissions.FacultyManage);

        faculties.MapPatch("/{id:guid}", async (Guid id, UpdateFacultyRequest body, string? lang, HttpContext httpContext, FacultyService service, CancellationToken cancellationToken) =>
        {
            var result = await service.UpdateAsync(id, body, httpContext.GetAuditContext(), lang, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(OrganizationPermissions.FacultyManage);

        faculties.MapPost("/{id:guid}/deactivate", async (Guid id, DeactivateRequestBody body, HttpContext httpContext, FacultyService service, CancellationToken cancellationToken) =>
        {
            var result = await service.DeactivateAsync(id, body.Version, httpContext.GetAuditContext(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(OrganizationPermissions.FacultyManage);
    }
}
