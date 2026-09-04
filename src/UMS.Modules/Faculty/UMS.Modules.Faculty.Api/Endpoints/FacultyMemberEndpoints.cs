using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Faculty.Application.FacultyMembers;
using UMS.Modules.Faculty.Application.Permissions;
using UMS.Shared.Authorization;
using UMS.Shared.ErrorHandling;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Faculty.Api.Endpoints;

/// <summary>FAC-1/FAC-2: FacultyMember employment-profile CRUD (requirement-spec.md faculty §6).</summary>
internal static class FacultyMemberEndpoints
{
    public static void MapFacultyMemberEndpoints(this RouteGroupBuilder group)
    {
        var members = group.MapGroup("/members");

        members.MapGet("/", async (Guid? departmentId, int? skip, int? take, FacultyMemberService service, CancellationToken cancellationToken) =>
        {
            var page = await service.ListAsync(departmentId, skip ?? 0, take ?? 50, cancellationToken).ConfigureAwait(false);
            return Results.Ok(page);
        }).RequirePermission(FacultyPermissions.ProfileRead);

        members.MapGet("/{id:guid}", async (Guid id, HttpContext httpContext, FacultyMemberService service, CancellationToken cancellationToken) =>
        {
            var result = await service.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(FacultyPermissions.ProfileRead);

        members.MapPost("/", async (OnboardFacultyMemberRequest body, HttpContext httpContext, FacultyMemberService service, CancellationToken cancellationToken) =>
        {
            var result = await service.OnboardAsync(body, httpContext.GetAuditContext(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(
                dto => Results.Created($"/api/v1/faculty/members/{dto.Id}", dto),
                error => error.ToProblemResult(httpContext));
        }).RequirePermission(FacultyPermissions.MemberManage);

        members.MapPatch("/{id:guid}/self-service", async (Guid id, UpdateSelfServiceProfileRequest body, HttpContext httpContext, FacultyMemberService service, CancellationToken cancellationToken) =>
        {
            var result = await service.UpdateSelfServiceAsync(id, httpContext.User.GetUserId(), body, httpContext.GetAuditContext(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequireLiveSession();

        members.MapPatch("/{id:guid}", async (Guid id, UpdateEmploymentDetailsRequest body, HttpContext httpContext, FacultyMemberService service, CancellationToken cancellationToken) =>
        {
            var result = await service.UpdateEmploymentDetailsAsync(id, body, httpContext.GetAuditContext(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(FacultyPermissions.MemberManage);

        members.MapPost("/{id:guid}/status", async (Guid id, ChangeFacultyMemberStatusRequest body, HttpContext httpContext, FacultyMemberService service, CancellationToken cancellationToken) =>
        {
            var result = await service.ChangeStatusAsync(id, body, httpContext.GetAuditContext(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(FacultyPermissions.MemberManage);
    }
}
