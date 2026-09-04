using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Student.Application.Guardians;
using UMS.Shared.Authorization;
using UMS.Shared.ErrorHandling;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Student.Api.Endpoints;

/// <summary>
/// Guardian/GuardianAccessGrant scaffolding (docs/ddd/ubiquitous-language.md) - not in
/// requirement-spec.md's §6 endpoint table (Guardian/GuardianAccessGrant are not decomposed into
/// tickets.md). Nested under <c>/students/me/guardians</c> - Student-owned only, no admin/HR
/// variant exists (a Guardian's consent is the Student's own to give and revoke). This module's
/// own PR description documents this route/data-shape choice as a first-pass design decision.
/// </summary>
internal static class GuardianEndpoints
{
    public static void MapGuardianEndpoints(this RouteGroupBuilder group)
    {
        var guardians = group.MapGroup("/students/me/guardians");

        guardians.MapGet("/", async (HttpContext httpContext, GuardianService service, CancellationToken cancellationToken) =>
        {
            var result = await service.ListOwnGuardiansAsync(httpContext.User.GetUserId(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequireLiveSession();

        guardians.MapPost("/", async (LinkGuardianRequest body, HttpContext httpContext, GuardianService service, CancellationToken cancellationToken) =>
        {
            var result = await service.LinkGuardianAsync(httpContext.User.GetUserId(), body, httpContext.GetAuditContext(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(
                dto => Results.Created($"/api/v1/student/students/me/guardians/{dto.Id}", dto),
                error => error.ToProblemResult(httpContext));
        }).RequireLiveSession();

        guardians.MapPost("/{guardianId:guid}/access-grants", async (Guid guardianId, GrantGuardianAccessRequest body, HttpContext httpContext, GuardianService service, CancellationToken cancellationToken) =>
        {
            var result = await service.GrantAccessAsync(httpContext.User.GetUserId(), guardianId, body, httpContext.GetAuditContext(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequireLiveSession();

        guardians.MapDelete("/{guardianId:guid}/access-grants/{category}", async (Guid guardianId, string category, HttpContext httpContext, GuardianService service, CancellationToken cancellationToken) =>
        {
            var result = await service.RevokeAccessAsync(httpContext.User.GetUserId(), guardianId, category, httpContext.GetAuditContext(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequireLiveSession();
    }
}
