using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Research.Application.InstitutionalRepositoryEntries;
using UMS.Modules.Research.Application.Permissions;
using UMS.Shared.Authorization;
using UMS.Shared.ErrorHandling;
using UMS.Shared.ErrorHandling.Results;
using UMS.Shared.Faculty;

namespace UMS.Modules.Research.Api.Endpoints;

/// <summary>RES-10..RES-12: requirement-spec.md §2 Institutional Repository, §6 API Surface Repository rows.</summary>
internal static class InstitutionalRepositoryEntryEndpoints
{
    public static void MapInstitutionalRepositoryEntryEndpoints(this RouteGroupBuilder group)
    {
        var entries = group.MapGroup("/repository-entries");

        entries.MapPost("/", async (DepositRepositoryEntryRequest body, HttpContext httpContext, InstitutionalRepositoryEntryService service, CancellationToken cancellationToken) =>
        {
            var result = await service.DepositAsync(body, httpContext.GetAuditContext(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(
                dto => Results.Created($"/api/v1/research/repository-entries/{dto.Id}", dto),
                error => error.ToProblemResult(httpContext));
        }).RequirePermission(ResearchPermissions.RepositoryDeposit);

        entries.MapGet("/{id:guid}", async (Guid id, HttpContext httpContext, InstitutionalRepositoryEntryService service, IFacultyMemberLookup facultyMemberLookup, IPermissionResolver permissionResolver, CancellationToken cancellationToken) =>
        {
            var result = await service.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
            if (result.IsFailure)
            {
                return result.Error!.ToProblemResult(httpContext);
            }

            if (!result.Value.Embargo.IsEmbargoed)
            {
                return Results.Ok(result.Value);
            }

            var guard = await EnsureDepositorAdvisorOrAdminAsync(result.Value, httpContext, facultyMemberLookup, permissionResolver, cancellationToken).ConfigureAwait(false);
            return guard ?? Results.Ok(result.Value);
        }).RequirePermission(ResearchPermissions.RepositoryRead);

        // requirement-spec.md §8: "Until lifted, only the depositor, the supervising FacultyMember,
        // and Admin can see the entry" - a page-level filter below (the service's own query stays
        // simple; a heavily embargo-populated result page may under-fill relative to `take`, an
        // accepted first-pass simplification for a low-traffic authenticated listing).
        entries.MapGet("/", async (Guid? supervisingFacultyMemberId, int? skip, int? take, HttpContext httpContext, InstitutionalRepositoryEntryService service, IFacultyMemberLookup facultyMemberLookup, IPermissionResolver permissionResolver, CancellationToken cancellationToken) =>
        {
            var page = await service.ListAsync(supervisingFacultyMemberId, skip ?? 0, take ?? 50, cancellationToken).ConfigureAwait(false);
            var isAdmin = await permissionResolver.CheckAsync(httpContext.User.GetUserId(), httpContext.User.GetSessionId(), ResearchPermissions.RepositoryManage, cancellationToken).ConfigureAwait(false) == PermissionCheckOutcome.Granted;
            if (isAdmin)
            {
                return Results.Ok(page);
            }

            var caller = await facultyMemberLookup.GetByUserIdAsync(httpContext.User.GetUserId(), cancellationToken).ConfigureAwait(false);
            var visible = page.Items.Where(e => !e.Embargo.IsEmbargoed
                || e.SupervisingFacultyMemberId == caller?.Id
                || (caller is not null && e.Depositor.FacultyMemberId == caller.Id)).ToList();
            return Results.Ok(page with { Items = visible });
        }).RequirePermission(ResearchPermissions.RepositoryRead);

        // RES-11: Admin-only manual override - the daily worker (RES-12) performs the automatic path.
        entries.MapPost("/{id:guid}/lift-embargo", async (Guid id, HttpContext httpContext, InstitutionalRepositoryEntryService service, CancellationToken cancellationToken) =>
        {
            var result = await service.LiftEmbargoAsync(id, httpContext.GetAuditContext(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(ResearchPermissions.RepositoryManage);

        // UMS.Shared.Documents.IUploadedArtifactRequester's presigned-upload flow (mechanism #12) -
        // any authenticated caller may request their own upload slot; the resulting artifactId is
        // only ever attached to an entry the caller may write to (checked at confirm-time below).
        entries.MapPost("/artifact-upload-url", async (RequestArtifactUploadUrlRequest body, HttpContext httpContext, InstitutionalRepositoryEntryService service, CancellationToken cancellationToken) =>
        {
            var result = await service.RequestArtifactUploadAsync(httpContext.User.GetUserId(), body.MimeType, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequireLiveSession();

        entries.MapPost("/{id:guid}/artifact", async (Guid id, ConfirmArtifactUploadRequest body, HttpContext httpContext, InstitutionalRepositoryEntryService service, IFacultyMemberLookup facultyMemberLookup, IPermissionResolver permissionResolver, CancellationToken cancellationToken) =>
        {
            var existing = await service.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
            if (existing.IsFailure)
            {
                return existing.Error!.ToProblemResult(httpContext);
            }

            var guard = await EnsureDepositorAdvisorOrAdminAsync(existing.Value, httpContext, facultyMemberLookup, permissionResolver, cancellationToken).ConfigureAwait(false);
            if (guard is not null)
            {
                return guard;
            }

            var result = await service.ConfirmArtifactUploadAsync(id, body.ArtifactId, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequireLiveSession();
    }

    private static async Task<IResult?> EnsureDepositorAdvisorOrAdminAsync(InstitutionalRepositoryEntryDto entry, HttpContext httpContext, IFacultyMemberLookup facultyMemberLookup, IPermissionResolver permissionResolver, CancellationToken cancellationToken)
    {
        var isAdmin = await permissionResolver.CheckAsync(httpContext.User.GetUserId(), httpContext.User.GetSessionId(), ResearchPermissions.RepositoryManage, cancellationToken).ConfigureAwait(false) == PermissionCheckOutcome.Granted;
        if (isAdmin)
        {
            return null;
        }

        var caller = await facultyMemberLookup.GetByUserIdAsync(httpContext.User.GetUserId(), cancellationToken).ConfigureAwait(false);
        var isDepositorOrAdvisor = caller is not null && (entry.Depositor.FacultyMemberId == caller.Id || entry.SupervisingFacultyMemberId == caller.Id);
        return isDepositorOrAdvisor
            ? null
            : Error.Forbidden("repositoryentry.not_authorized", "You are not this entry's depositor or supervising FacultyMember and do not hold Admin access.").ToProblemResult(httpContext);
    }
}

public sealed record RequestArtifactUploadUrlRequest(string MimeType);

public sealed record ConfirmArtifactUploadRequest(Guid ArtifactId);
