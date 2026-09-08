using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Alumni.Application.Abstractions;
using UMS.Modules.Alumni.Application.Alumni;
using UMS.Modules.Alumni.Application.Common;
using UMS.Modules.Alumni.Application.Permissions;
using UMS.Modules.Alumni.Domain.Alumni;
using UMS.Shared.Authorization;
using UMS.Shared.ErrorHandling;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Alumni.Api.Endpoints;

/// <summary>ALM-1/ALM-2/ALM-3: requirement-spec.md §6 <c>/alumni/profile</c>, <c>/alumni/directory</c>.</summary>
internal static class AlumnusEndpoints
{
    public static void MapAlumnusEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/profile", async (HttpContext httpContext, CallerAlumnusResolver resolver, CancellationToken cancellationToken) =>
        {
            var alumnus = await resolver.ResolveAsync(httpContext.User.GetUserId(), cancellationToken).ConfigureAwait(false);
            return alumnus is null
                ? Error.NotFound("alumnus.not_found", "No Alumnus record is linked to your account.").ToProblemResult(httpContext)
                : Results.Ok(AlumnusService.ToDto(alumnus));
        }).RequireLiveSession();

        group.MapPut("/profile", async (UpdateOwnProfileRequest body, HttpContext httpContext, CallerAlumnusResolver resolver, AlumnusService service, CancellationToken cancellationToken) =>
        {
            var alumnus = await resolver.ResolveAsync(httpContext.User.GetUserId(), cancellationToken).ConfigureAwait(false);
            if (alumnus is null)
            {
                return Error.NotFound("alumnus.not_found", "No Alumnus record is linked to your account.").ToProblemResult(httpContext);
            }

            var profileResult = await service.UpdateProfileAsync(
                alumnus.Id.Value,
                new UpdateAlumnusProfileRequest(body.CurrentEmployer, body.Bio, body.Location, body.ContactEmail, body.ContactPhone, body.HideCurrentEmployer, body.HideContactDetails),
                cancellationToken).ConfigureAwait(false);
            if (profileResult.IsFailure)
            {
                return profileResult.Error!.ToProblemResult(httpContext);
            }

            if (body.Visibility is null)
            {
                return Results.Ok(profileResult.Value);
            }

            if (!Enum.TryParse<ProfileVisibility>(body.Visibility, ignoreCase: true, out var visibility))
            {
                return Error.Validation("alumnus.invalid_visibility", $"'{body.Visibility}' is not a recognized ProfileVisibility.").ToProblemResult(httpContext);
            }

            var visibilityResult = await service.SetVisibilityAsync(alumnus.Id.Value, visibility, audit: null, cancellationToken).ConfigureAwait(false);
            return visibilityResult.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequireLiveSession();

        // requirement-spec.md §5/§7: an Admin override of directory visibility is itself an audited access.
        group.MapPost("/directory/{alumnusId:guid}/visibility", async (Guid alumnusId, SetProfileVisibilityRequest body, HttpContext httpContext, AlumnusService service, CancellationToken cancellationToken) =>
        {
            if (!Enum.TryParse<ProfileVisibility>(body.Visibility, ignoreCase: true, out var visibility))
            {
                return Error.Validation("alumnus.invalid_visibility", $"'{body.Visibility}' is not a recognized ProfileVisibility.").ToProblemResult(httpContext);
            }

            var result = await service.SetVisibilityAsync(alumnusId, visibility, httpContext.GetAuditContext(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(AlumniPermissions.DirectoryReadPrivate);

        // requirement-spec.md §2.2/§4: Public-only for an ordinary caller, full bypass for Admin/support (audited read access is a first-pass call - this GET itself is not separately audited, only the visibility-override WRITE above is, matching §5's literal wording).
        group.MapGet("/directory", async (int? graduationYear, Guid? programId, Guid? departmentId, Guid? chapterId, string? employer, string? location, int? skip, int? take, HttpContext httpContext, AlumnusService service, CallerAlumnusResolver resolver, UMS.Shared.Authorization.IPermissionResolver permissionResolver, CancellationToken cancellationToken) =>
        {
            var isPrivileged = await permissionResolver.CheckAsync(httpContext.User.GetUserId(), httpContext.User.GetSessionId(), AlumniPermissions.DirectoryReadPrivate, cancellationToken).ConfigureAwait(false) == UMS.Shared.Authorization.PermissionCheckOutcome.Granted;
            var caller = await resolver.ResolveAsync(httpContext.User.GetUserId(), cancellationToken).ConfigureAwait(false);

            var filter = new AlumniDirectoryFilter(graduationYear, programId, departmentId, chapterId, employer, location);
            var page = await service.SearchDirectoryAsync(filter, caller?.Id.Value, isPrivileged, skip ?? 0, take ?? 50, cancellationToken).ConfigureAwait(false);
            return Results.Ok(page);
        }).RequireLiveSession();
    }
}

/// <param name="Visibility">Optional - when supplied, also updates the record-level ProfileVisibility (self-service; an Admin override of ANOTHER alumnus's visibility uses the dedicated audited endpoint instead).</param>
internal sealed record UpdateOwnProfileRequest(string? CurrentEmployer, string? Bio, string? Location, string? ContactEmail, string? ContactPhone, bool HideCurrentEmployer, bool HideContactDetails, string? Visibility);
