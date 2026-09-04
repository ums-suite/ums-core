using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Faculty.Application.Permissions;
using UMS.Modules.Faculty.Application.ResearchProfiles;
using UMS.Shared.Authorization;
using UMS.Shared.ErrorHandling;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Faculty.Api.Endpoints;

/// <summary>FAC-13: ResearchProfile read (public) + write (owning FacultyMember or HR - requirement-spec.md faculty §2, §6).</summary>
internal static class ResearchProfileEndpoints
{
    public static void MapResearchProfileEndpoints(this RouteGroupBuilder group)
    {
        var researchProfiles = group.MapGroup("/members/{facultyMemberId:guid}/research-profile");

        // Publicly surfaced (requirement-spec.md §2 Research Profile: "Publicly surfaced on the
        // Public Website ... via Faculty's own public query interface") - no permission gate.
        researchProfiles.MapGet("/", async (Guid facultyMemberId, HttpContext httpContext, ResearchProfileService service, CancellationToken cancellationToken) =>
        {
            var result = await service.GetByFacultyMemberIdAsync(facultyMemberId, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).AllowAnonymous();

        researchProfiles.MapPut("/", async (Guid facultyMemberId, UpdateResearchProfileRequest body, HttpContext httpContext, ResearchProfileService service, IPermissionResolver permissionResolver, CancellationToken cancellationToken) =>
        {
            // requirement-spec.md §2 Permission Strings: `faculty.research.update` (HR, may write
            // any FacultyMember's profile) and `faculty.research.publish` (the owning FacultyMember
            // publishing their own) - both gate this one endpoint, distinguished by which holder is
            // calling; `ResearchProfileService.UpdateAsync`'s own ownership check is still the
            // authoritative "is this really the owner" guard even for a `ResearchPublish` holder.
            var isPrivileged = await permissionResolver.HasPermissionAsync(httpContext.User, FacultyPermissions.ResearchUpdate, cancellationToken).ConfigureAwait(false);
            var canPublishOwn = isPrivileged || await permissionResolver.HasPermissionAsync(httpContext.User, FacultyPermissions.ResearchPublish, cancellationToken).ConfigureAwait(false);
            if (!canPublishOwn)
            {
                return Error.Forbidden("researchprofile.permission_denied", "Requires 'faculty.research.update' or 'faculty.research.publish'.").ToProblemResult(httpContext);
            }

            var result = await service.UpdateAsync(facultyMemberId, httpContext.User.GetUserId(), isPrivileged, body, httpContext.GetAuditContext(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequireLiveSession();
    }
}
