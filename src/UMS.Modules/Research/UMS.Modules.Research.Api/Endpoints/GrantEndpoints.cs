using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Research.Application.Grants;
using UMS.Modules.Research.Application.Permissions;
using UMS.Shared.Authorization;
using UMS.Shared.ErrorHandling;
using UMS.Shared.ErrorHandling.Results;
using UMS.Shared.Faculty;

namespace UMS.Modules.Research.Api.Endpoints;

/// <summary>RES-2..RES-5: requirement-spec.md §2 Grant Lifecycle, §6 API Surface Grant rows.</summary>
internal static class GrantEndpoints
{
    public static void MapGrantEndpoints(this RouteGroupBuilder group)
    {
        var grants = group.MapGroup("/grants");

        // requirement-spec.md §6: "Propose a Grant; PI (self) or Admin/Research-Office" - a PI's own
        // role bundle carries this Permission for self-service proposal, same as Admin/Research-Office's.
        grants.MapPost("/", async (ProposeGrantRequest body, HttpContext httpContext, GrantService service, CancellationToken cancellationToken) =>
        {
            var result = await service.ProposeAsync(body, httpContext.GetAuditContext(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(
                dto => Results.Created($"/api/v1/research/grants/{dto.Id}", dto),
                error => error.ToProblemResult(httpContext));
        }).RequirePermission(ResearchPermissions.GrantPropose);

        grants.MapGet("/{id:guid}", async (Guid id, HttpContext httpContext, GrantService service, IFacultyMemberLookup facultyMemberLookup, IPermissionResolver permissionResolver, CancellationToken cancellationToken) =>
        {
            var result = await service.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
            if (result.IsFailure)
            {
                return result.Error!.ToProblemResult(httpContext);
            }

            var scopeGuard = await EnsureReadScopeAsync(result.Value, httpContext, facultyMemberLookup, permissionResolver, cancellationToken).ConfigureAwait(false);
            return scopeGuard ?? Results.Ok(result.Value);
        }).RequirePermission(ResearchPermissions.GrantRead);

        // requirement-spec.md §6: "Ownership/role-scoped for the PI/Co-I's own view; unrestricted for
        // Admin" - a caller without GrantManage (i.e. not Research-Office/Admin) is forced to their
        // own resolved FacultyMemberId regardless of any client-supplied filter.
        grants.MapGet("/", async (Guid? principalInvestigatorId, Guid? facultyMemberId, string? status, int? skip, int? take, HttpContext httpContext, GrantService service, IFacultyMemberLookup facultyMemberLookup, IPermissionResolver permissionResolver, CancellationToken cancellationToken) =>
        {
            var isAdmin = await permissionResolver.CheckAsync(httpContext.User.GetUserId(), httpContext.User.GetSessionId(), ResearchPermissions.GrantManage, cancellationToken).ConfigureAwait(false) == PermissionCheckOutcome.Granted;

            var effectiveFacultyMemberId = facultyMemberId;
            var effectivePiId = principalInvestigatorId;
            if (!isAdmin)
            {
                var caller = await facultyMemberLookup.GetByUserIdAsync(httpContext.User.GetUserId(), cancellationToken).ConfigureAwait(false);
                if (caller is null)
                {
                    return Results.Ok(new GrantListPage([], skip ?? 0, take ?? 50));
                }

                effectiveFacultyMemberId = caller.Id;
                effectivePiId = null;
            }

            return Results.Ok(await service.ListAsync(effectivePiId, effectiveFacultyMemberId, status, skip ?? 0, take ?? 50, cancellationToken).ConfigureAwait(false));
        }).RequirePermission(ResearchPermissions.GrantRead);

        // §6: "fund ... reserved to Research-Office/Admin" - no PI dual path.
        grants.MapPost("/{id:guid}/fund", async (Guid id, FundGrantRequest body, HttpContext httpContext, GrantService service, CancellationToken cancellationToken) =>
        {
            var result = await service.FundAsync(id, body, httpContext.GetAuditContext(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(ResearchPermissions.GrantManage);

        // §6: "activate ... also available to the PI".
        grants.MapPost("/{id:guid}/activate", async (Guid id, GrantVersionedActionRequest body, HttpContext httpContext, GrantService service, IFacultyMemberLookup facultyMemberLookup, IPermissionResolver permissionResolver, CancellationToken cancellationToken) =>
        {
            var guard = await EnsureOwningPiOrManageAsync(id, httpContext, service, facultyMemberLookup, permissionResolver, cancellationToken).ConfigureAwait(false);
            if (guard is not null)
            {
                return guard;
            }

            var result = await service.ActivateAsync(id, body.Version, httpContext.GetAuditContext(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequireLiveSession();

        // §6: "close ... also available to the PI".
        grants.MapPost("/{id:guid}/close", async (Guid id, GrantVersionedActionRequest body, HttpContext httpContext, GrantService service, IFacultyMemberLookup facultyMemberLookup, IPermissionResolver permissionResolver, CancellationToken cancellationToken) =>
        {
            var guard = await EnsureOwningPiOrManageAsync(id, httpContext, service, facultyMemberLookup, permissionResolver, cancellationToken).ConfigureAwait(false);
            if (guard is not null)
            {
                return guard;
            }

            var result = await service.CloseAsync(id, body.Version, httpContext.GetAuditContext(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequireLiveSession();

        // §6: "report ... reserved to Research-Office/Admin" - Grant.Report's own domain remarks note
        // a PI is the natural author of a funding-body report; Research-Office/Admin recording it on
        // the PI's behalf mirrors Propose's own "PI (self) or Admin" posture, so GrantManage alone
        // gates this (no separate PI-only path is needed - a PI holding GrantManage already covers
        // the self-service case; a PI who does NOT hold it works through Research-Office/Admin,
        // exactly as the spec table's "reserved to Research-Office/Admin" wording states).
        grants.MapPost("/{id:guid}/report", async (Guid id, GrantVersionedActionRequest body, HttpContext httpContext, GrantService service, CancellationToken cancellationToken) =>
        {
            var result = await service.ReportAsync(id, body.Version, httpContext.GetAuditContext(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(ResearchPermissions.GrantManage);

        grants.MapPost("/{id:guid}/reject", async (Guid id, GrantVersionedActionRequest body, HttpContext httpContext, GrantService service, CancellationToken cancellationToken) =>
        {
            var result = await service.RejectAsync(id, body.Version, httpContext.GetAuditContext(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(ResearchPermissions.GrantManage);

        // requirement-spec.md §2: "Withdrawn reachable from Proposed/Funded/Active (PI or institution
        // withdraws)" - dual path, same shape as activate/close.
        grants.MapPost("/{id:guid}/withdraw", async (Guid id, GrantVersionedActionRequest body, HttpContext httpContext, GrantService service, IFacultyMemberLookup facultyMemberLookup, IPermissionResolver permissionResolver, CancellationToken cancellationToken) =>
        {
            var guard = await EnsureOwningPiOrManageAsync(id, httpContext, service, facultyMemberLookup, permissionResolver, cancellationToken).ConfigureAwait(false);
            if (guard is not null)
            {
                return guard;
            }

            var result = await service.WithdrawAsync(id, body.Version, httpContext.GetAuditContext(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequireLiveSession();

        // §6: "Add/remove Co-Investigator or reassign PI; PI-vacancy invariant enforced" - the
        // ResearchPermissionManifest's own remarks: "alongside the owning PI".
        grants.MapPost("/{id:guid}/investigators", async (Guid id, AddGrantInvestigatorRequest body, HttpContext httpContext, GrantService service, IFacultyMemberLookup facultyMemberLookup, IPermissionResolver permissionResolver, CancellationToken cancellationToken) =>
        {
            var guard = await EnsureOwningPiOrPermissionAsync(id, httpContext, service, facultyMemberLookup, permissionResolver, ResearchPermissions.GrantInvestigatorsManage, cancellationToken).ConfigureAwait(false);
            if (guard is not null)
            {
                return guard;
            }

            var result = await service.AddInvestigatorAsync(id, body, httpContext.GetAuditContext(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequireLiveSession();

        // Minimal APIs never infer a body parameter for MapDelete - [FromBody] is required
        // explicitly (mirrors Academic's own EnrollmentEndpoints.MapDelete precedent exactly).
        grants.MapDelete("/{id:guid}/investigators/{facultyMemberId:guid}", async (Guid id, Guid facultyMemberId, [FromBody] GrantVersionedActionRequest body, HttpContext httpContext, GrantService service, IFacultyMemberLookup facultyMemberLookup, IPermissionResolver permissionResolver, CancellationToken cancellationToken) =>
        {
            var guard = await EnsureOwningPiOrPermissionAsync(id, httpContext, service, facultyMemberLookup, permissionResolver, ResearchPermissions.GrantInvestigatorsManage, cancellationToken).ConfigureAwait(false);
            if (guard is not null)
            {
                return guard;
            }

            var result = await service.RemoveInvestigatorAsync(id, facultyMemberId, body.Version, httpContext.GetAuditContext(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequireLiveSession();

        // Admin-only convenience beyond §6's literal API surface table - toggles whether an
        // otherwise-eligible Grant appears on the public showcase (e.g. a confidential industry
        // grant, requirement-spec.md §2 Public Research Showcase).
        grants.MapPost("/{id:guid}/publicly-visible", async (Guid id, SetPubliclyVisibleRequest body, HttpContext httpContext, GrantService service, CancellationToken cancellationToken) =>
        {
            var result = await service.SetPubliclyVisibleAsync(id, body.IsPubliclyVisible, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(ResearchPermissions.GrantManage);
    }

    private static async Task<IResult?> EnsureReadScopeAsync(GrantDto grant, HttpContext httpContext, IFacultyMemberLookup facultyMemberLookup, IPermissionResolver permissionResolver, CancellationToken cancellationToken)
    {
        var isAdmin = await permissionResolver.CheckAsync(httpContext.User.GetUserId(), httpContext.User.GetSessionId(), ResearchPermissions.GrantManage, cancellationToken).ConfigureAwait(false) == PermissionCheckOutcome.Granted;
        if (isAdmin)
        {
            return null;
        }

        var caller = await facultyMemberLookup.GetByUserIdAsync(httpContext.User.GetUserId(), cancellationToken).ConfigureAwait(false);
        var isInvestigator = caller is not null && grant.Investigators.Any(i => i.FacultyMemberId == caller.Id);
        return isInvestigator
            ? null
            : Error.Forbidden("grant.read_forbidden", "You are not an investigator on this Grant and do not hold Research-Office/Admin access.").ToProblemResult(httpContext);
    }

    private static async Task<IResult?> EnsureOwningPiOrManageAsync(Guid grantId, HttpContext httpContext, GrantService service, IFacultyMemberLookup facultyMemberLookup, IPermissionResolver permissionResolver, CancellationToken cancellationToken)
    {
        var grant = await service.GetByIdAsync(grantId, cancellationToken).ConfigureAwait(false);
        if (grant.IsFailure)
        {
            return grant.Error!.ToProblemResult(httpContext);
        }

        return await GrantOwnershipGuard.EnsureOwningPiOrPermissionAsync(grant.Value, httpContext, facultyMemberLookup, permissionResolver, ResearchPermissions.GrantManage, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<IResult?> EnsureOwningPiOrPermissionAsync(Guid grantId, HttpContext httpContext, GrantService service, IFacultyMemberLookup facultyMemberLookup, IPermissionResolver permissionResolver, string staffPermission, CancellationToken cancellationToken)
    {
        var grant = await service.GetByIdAsync(grantId, cancellationToken).ConfigureAwait(false);
        if (grant.IsFailure)
        {
            return grant.Error!.ToProblemResult(httpContext);
        }

        return await GrantOwnershipGuard.EnsureOwningPiOrPermissionAsync(grant.Value, httpContext, facultyMemberLookup, permissionResolver, staffPermission, cancellationToken).ConfigureAwait(false);
    }
}
