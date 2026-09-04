using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Organization.Api.Contracts;
using UMS.Modules.Organization.Application.Departments;
using UMS.Modules.Organization.Application.Permissions;
using UMS.Shared.Authorization;
using UMS.Shared.ErrorHandling;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Organization.Api.Endpoints;

/// <summary>ORG-4: Department CRUD + deactivate (requirement-spec.md organization §6).</summary>
internal static class DepartmentEndpoints
{
    public static void MapDepartmentEndpoints(this RouteGroupBuilder group)
    {
        var departments = group.MapGroup("/departments");

        departments.MapGet("/", async (Guid? facultyId, int? skip, int? take, string? lang, DepartmentService service, CancellationToken cancellationToken) =>
        {
            var page = await service.ListAsync(facultyId, skip ?? 0, take ?? 50, lang, cancellationToken).ConfigureAwait(false);
            return Results.Ok(page);
        }).AllowAnonymous();

        departments.MapGet("/{id:guid}", async (Guid id, string? lang, HttpContext httpContext, DepartmentService service, CancellationToken cancellationToken) =>
        {
            var result = await service.GetByIdAsync(id, lang, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).AllowAnonymous();

        departments.MapPost("/", async (CreateDepartmentRequest body, string? lang, HttpContext httpContext, DepartmentService service, CancellationToken cancellationToken) =>
        {
            var result = await service.CreateAsync(body, httpContext.GetAuditContext(), lang, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(
                dto => Results.Created($"/api/v1/organization/departments/{dto.Id}", dto),
                error => error.ToProblemResult(httpContext));
        }).RequirePermission(OrganizationPermissions.DepartmentManage);

        departments.MapPatch("/{id:guid}", async (Guid id, UpdateDepartmentRequest body, string? lang, HttpContext httpContext, DepartmentService service, CancellationToken cancellationToken) =>
        {
            var result = await service.UpdateAsync(id, body, httpContext.GetAuditContext(), lang, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(OrganizationPermissions.DepartmentManage);

        departments.MapPost("/{id:guid}/deactivate", async (Guid id, DeactivateRequestBody body, HttpContext httpContext, DepartmentService service, CancellationToken cancellationToken) =>
        {
            var result = await service.DeactivateAsync(id, body.Version, httpContext.GetAuditContext(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(OrganizationPermissions.DepartmentManage);
    }
}
