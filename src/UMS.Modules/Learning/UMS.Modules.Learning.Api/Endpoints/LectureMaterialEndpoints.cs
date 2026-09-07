using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Learning.Application.LectureMaterials;
using UMS.Modules.Learning.Application.Permissions;
using UMS.Shared.Authorization;
using UMS.Shared.ErrorHandling;

namespace UMS.Modules.Learning.Api.Endpoints;

/// <summary>
/// LRN-12/LRN-13/LRN-14/LRN-15: requirement-spec.md learning §6's LectureMaterial rows.
///
/// <para>
/// <b>Documented deviation from §6's literal table:</b> two upload endpoints
/// (<c>POST /course-offerings/{id}/lecture-materials/uploads</c> and <c>.../uploads/confirm</c>) are
/// added for exactly the same reason their <c>Submission</c> counterparts are - see
/// <see cref="SubmissionEndpoints"/>' own remarks. §5 requires a multi-hundred-MB video to reach
/// object storage without passing through the API tier, which needs a presigned URL issued before
/// the material is created; §6's table lists only the create/version endpoints. Documents' own
/// upload endpoints are permission-gated for a Registrar-level capability, so these are Learning's
/// Instructor-gated equivalents over the same shared contract.
/// </para>
///
/// <para>
/// <b>Documented deviation, second:</b> <c>GET /course-offerings/{id}/lecture-materials</c> accepts
/// an optional <c>?lang=</c> query parameter, matching Organization's own already-merged
/// localization convention (ums-conventions.md's Localization Implementation, ADR-0011) rather than
/// §6's table, which names no language parameter for a resource whose §2 explicitly requires
/// bilingual title/description metadata.
/// </para>
/// </summary>
internal static class LectureMaterialEndpoints
{
    public static void MapLectureMaterialEndpoints(this RouteGroupBuilder group)
    {
        var offeringMaterials = group.MapGroup("/course-offerings/{courseOfferingId:guid}/lecture-materials");

        offeringMaterials.MapPost("/uploads", async (Guid courseOfferingId, RequestLectureMaterialUploadRequest body, HttpContext httpContext, LectureMaterialService service, CancellationToken cancellationToken) =>
        {
            var result = await service.RequestUploadAsync(courseOfferingId, httpContext.User.GetUserId(), body, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(LearningPermissions.LectureMaterialManage);

        offeringMaterials.MapPost("/uploads/confirm", async (Guid courseOfferingId, ConfirmLectureMaterialUploadRequest body, HttpContext httpContext, LectureMaterialService service, CancellationToken cancellationToken) =>
        {
            var result = await service.ConfirmUploadAsync(courseOfferingId, httpContext.User.GetUserId(), body, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(LearningPermissions.LectureMaterialManage);

        offeringMaterials.MapPost("/", async (Guid courseOfferingId, CreateLectureMaterialRequest body, HttpContext httpContext, LectureMaterialService service, CancellationToken cancellationToken) =>
        {
            var result = await service.PublishAsync(courseOfferingId, httpContext.User.GetUserId(), body, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(
                dto => Results.Created($"/api/v1/learning/lecture-materials/{dto.Id}", dto),
                error => error.ToProblemResult(httpContext));
        }).RequirePermission(LearningPermissions.LectureMaterialManage);

        offeringMaterials.MapGet("/", async (Guid courseOfferingId, string? lang, HttpContext httpContext, LectureMaterialQueryService service, CancellationToken cancellationToken) =>
        {
            var result = await service.ListByCourseOfferingAsync(courseOfferingId, httpContext.User.GetUserId(), lang, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequireLiveSession();

        var materials = group.MapGroup("/lecture-materials");

        materials.MapPost("/{id:guid}/versions", async (Guid id, PublishLectureMaterialVersionRequest body, HttpContext httpContext, LectureMaterialService service, CancellationToken cancellationToken) =>
        {
            var result = await service.PublishVersionAsync(id, httpContext.User.GetUserId(), body, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(LearningPermissions.LectureMaterialManage);

        // LRN-14's other half: explicit prior-version access. A version published before the
        // current one is served exactly as it was published - requirement-spec.md §4's
        // "every prior version stays individually addressable".
        materials.MapGet("/{id:guid}/versions/{versionId:guid}", async (Guid id, Guid versionId, HttpContext httpContext, LectureMaterialQueryService service, CancellationToken cancellationToken) =>
        {
            var result = await service.GetVersionAsync(id, versionId, httpContext.User.GetUserId(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequireLiveSession();
    }
}
