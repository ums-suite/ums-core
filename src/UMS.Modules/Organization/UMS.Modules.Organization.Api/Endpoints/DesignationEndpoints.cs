using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Organization.Application.Designations;
using UMS.Modules.Organization.Application.Permissions;
using UMS.Shared.Authorization;
using UMS.Shared.ErrorHandling;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Organization.Api.Endpoints;

/// <summary>ORG-6: Designation CRUD (requirement-spec.md organization §6) - create-and-list only.</summary>
internal static class DesignationEndpoints
{
    public static void MapDesignationEndpoints(this RouteGroupBuilder group)
    {
        var designations = group.MapGroup("/designations");

        designations.MapGet("/", async (int? skip, int? take, string? lang, DesignationService service, CancellationToken cancellationToken) =>
        {
            var page = await service.ListAsync(skip ?? 0, take ?? 50, lang, cancellationToken).ConfigureAwait(false);
            return Results.Ok(page);
        }).AllowAnonymous();

        designations.MapGet("/{id:guid}", async (Guid id, string? lang, HttpContext httpContext, DesignationService service, CancellationToken cancellationToken) =>
        {
            var result = await service.GetByIdAsync(id, lang, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).AllowAnonymous();

        designations.MapPost("/", async (CreateDesignationRequest body, string? lang, HttpContext httpContext, DesignationService service, CancellationToken cancellationToken) =>
        {
            var result = await service.CreateAsync(body, httpContext.GetAuditContext(), lang, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(
                dto => Results.Created($"/api/v1/organization/designations/{dto.Id}", dto),
                error => error.ToProblemResult(httpContext));
        }).RequirePermission(OrganizationPermissions.DesignationManage);
    }
}
