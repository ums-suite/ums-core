using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Research.Application.Permissions;
using UMS.Modules.Research.Application.Publications;
using UMS.Shared.Authorization;
using UMS.Shared.ErrorHandling;
using UMS.Shared.ErrorHandling.Results;
using UMS.Shared.Faculty;

namespace UMS.Modules.Research.Api.Endpoints;

/// <summary>RES-6..RES-9: requirement-spec.md §2 Publication Records, §6 API Surface Publication rows.</summary>
internal static class PublicationEndpoints
{
    public static void MapPublicationEndpoints(this RouteGroupBuilder group)
    {
        var publications = group.MapGroup("/publications");

        // "Any listed internal AuthorEntry's FacultyMember, or Admin" - PublicationMerge (Admin-only
        // elsewhere in this module) doubles as the staff-override permission here.
        publications.MapPost("/", async (CreatePublicationRequest body, HttpContext httpContext, PublicationService service, IFacultyMemberLookup facultyMemberLookup, IPermissionResolver permissionResolver, CancellationToken cancellationToken) =>
        {
            var guard = await PublicationOwnershipGuard.EnsureAuthorOrPermissionAsync(body.Authors, httpContext, facultyMemberLookup, permissionResolver, ResearchPermissions.PublicationMerge, cancellationToken).ConfigureAwait(false);
            if (guard is not null)
            {
                return guard;
            }

            var result = await service.CreateAsync(body, httpContext.GetAuditContext(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(
                dto => Results.Created($"/api/v1/research/publications/{dto.Id}", dto),
                error => error.ToProblemResult(httpContext));
        }).RequirePermission(ResearchPermissions.PublicationCreate);

        publications.MapGet("/{id:guid}", async (Guid id, HttpContext httpContext, PublicationService service, CancellationToken cancellationToken) =>
        {
            var result = await service.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(ResearchPermissions.PublicationRead);

        publications.MapGet("/", async (Guid? authorFacultyMemberId, Guid? grantId, int? skip, int? take, PublicationService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.ListAsync(authorFacultyMemberId, grantId, skip ?? 0, take ?? 50, cancellationToken).ConfigureAwait(false)))
            .RequirePermission(ResearchPermissions.PublicationRead);

        publications.MapPut("/{id:guid}", async (Guid id, UpdatePublicationRequest body, HttpContext httpContext, PublicationService service, IFacultyMemberLookup facultyMemberLookup, IPermissionResolver permissionResolver, CancellationToken cancellationToken) =>
        {
            var existing = await service.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
            if (existing.IsFailure)
            {
                return existing.Error!.ToProblemResult(httpContext);
            }

            var guard = await PublicationOwnershipGuard.EnsureAuthorOrPermissionAsync(existing.Value.Authors, httpContext, facultyMemberLookup, permissionResolver, ResearchPermissions.PublicationMerge, cancellationToken).ConfigureAwait(false);
            if (guard is not null)
            {
                return guard;
            }

            var result = await service.UpdateAsync(id, body, httpContext.GetAuditContext(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(ResearchPermissions.PublicationUpdate);

        // RES-9: Admin-only duplicate resolution.
        publications.MapPost("/{id:guid}/merge", async (Guid id, MergePublicationsRequest body, HttpContext httpContext, PublicationService service, CancellationToken cancellationToken) =>
        {
            var result = await service.MergeAsync(id, body, httpContext.GetAuditContext(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(ResearchPermissions.PublicationMerge);

        // RES-9: the Admin review queue - not part of §6's literal API surface table, but required
        // for a human to actually act on a flagged PublicationDuplicateCandidate (§3, §9).
        publications.MapGet("/duplicate-candidates", async (int? skip, int? take, PublicationDuplicateDetectionService duplicateDetection, CancellationToken cancellationToken) =>
            Results.Ok(await duplicateDetection.ListPendingAsync(skip ?? 0, take ?? 50, cancellationToken).ConfigureAwait(false)))
            .RequirePermission(ResearchPermissions.PublicationMerge);

        publications.MapPost("/duplicate-candidates/{candidateId:guid}/dismiss", async (Guid candidateId, PublicationDuplicateDetectionService duplicateDetection, HttpContext httpContext, CancellationToken cancellationToken) =>
        {
            var result = await duplicateDetection.DismissAsync(candidateId, cancellationToken).ConfigureAwait(false);
            return result.IsSuccess ? Results.NoContent() : result.Error!.ToProblemResult(httpContext);
        }).RequirePermission(ResearchPermissions.PublicationMerge);

        // RES-8: fundedByGrantIds is not part of Create/Update's own request shape (never an object
        // graph load, §3/§9) - linked/unlinked one Grant id at a time instead.
        publications.MapPost("/{id:guid}/funded-by-grants", async (Guid id, LinkFundedByGrantRequest body, HttpContext httpContext, PublicationService service, CancellationToken cancellationToken) =>
        {
            var result = await service.AddFundedByGrantAsync(id, body.GrantId, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(ResearchPermissions.PublicationUpdate);

        publications.MapDelete("/{id:guid}/funded-by-grants/{grantId:guid}", async (Guid id, Guid grantId, HttpContext httpContext, PublicationService service, CancellationToken cancellationToken) =>
        {
            var result = await service.RemoveFundedByGrantAsync(id, grantId, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(ResearchPermissions.PublicationUpdate);

        // Admin-only convenience beyond §6's literal API surface table - see GrantEndpoints' own
        // publicly-visible endpoint remarks.
        publications.MapPost("/{id:guid}/publicly-visible", async (Guid id, SetPubliclyVisibleRequest body, HttpContext httpContext, PublicationService service, CancellationToken cancellationToken) =>
        {
            var result = await service.SetPubliclyVisibleAsync(id, body.IsPubliclyVisible, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(ResearchPermissions.PublicationMerge);
    }
}
