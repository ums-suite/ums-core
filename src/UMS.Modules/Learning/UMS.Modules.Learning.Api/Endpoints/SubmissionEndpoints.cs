using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Learning.Application.Permissions;
using UMS.Modules.Learning.Application.PlagiarismChecks;
using UMS.Modules.Learning.Application.Submissions;
using UMS.Shared.Authorization;
using UMS.Shared.ErrorHandling;

namespace UMS.Modules.Learning.Api.Endpoints;

/// <summary>
/// LRN-5/LRN-6/LRN-7/LRN-8/LRN-9/LRN-10/LRN-11: requirement-spec.md learning §6's Submission and
/// PlagiarismCheck rows.
///
/// <para>
/// <b>Documented deviation from §6's literal table:</b> two endpoints exist here that the table does
/// not list - <c>POST /assignments/{id}/submissions/uploads</c> and
/// <c>POST /assignments/{id}/submissions/uploads/confirm</c>. §6 lists only
/// <c>POST /assignments/{id}/submissions</c>, while §7.1 and design-decisions.md's "Raw File Storage
/// via Documents' Object-Storage Integration" simultaneously require that a Submission's file bytes
/// go <b>directly from the client to object storage via a presigned URL</b>, bypassing the API tier
/// entirely. Those two requirements cannot both hold with a single endpoint: a Student needs a URL
/// to upload to <i>before</i> they can name an <c>artifactId</c> in the submission body. These are
/// the request/confirm halves of the flow §7.1 itself spells out ("<c>POST /api/v1/documents/uploads</c>
/// requests a presigned upload URL... <c>.../confirm</c> triggers checksum verification"), surfaced
/// under Learning's own route because Documents' equivalents are gated behind a Registrar-level
/// permission a Student will never hold. Same posture as Notifications' own template-create endpoint
/// addition and Academic's <c>/results/{id}/...</c> route grouping: an explicit, documented
/// extension, never a silent one.
/// </para>
///
/// <para>
/// <b>Documented deviation, second:</b> <c>GET /assignments/{id}/submissions</c> takes an optional
/// <c>includeSuperseded</c> query flag §6's table does not mention. The superseded-chain decision
/// makes "the queue" ambiguous without it - default <c>false</c> gives the Instructor the counted
/// attempt per Student that decision defines, and <c>true</c> exposes the full retained history
/// requirement-spec.md §4 promises stays "permanently retrievable".
/// </para>
/// </summary>
internal static class SubmissionEndpoints
{
    public static void MapSubmissionEndpoints(this RouteGroupBuilder group)
    {
        var assignmentSubmissions = group.MapGroup("/assignments/{assignmentId:guid}/submissions");

        // LRN-5: presigned-upload request/confirm - Student-owned, gated by this module's own
        // enrollment + submission-window checks rather than by a Documents permission.
        assignmentSubmissions.MapPost("/uploads", async (Guid assignmentId, RequestSubmissionUploadRequest body, HttpContext httpContext, SubmissionUploadService service, CancellationToken cancellationToken) =>
        {
            var result = await service.RequestUploadAsync(assignmentId, httpContext.User.GetUserId(), body, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequireLiveSession();

        assignmentSubmissions.MapPost("/uploads/confirm", async (Guid assignmentId, ConfirmSubmissionUploadRequest body, HttpContext httpContext, SubmissionUploadService service, CancellationToken cancellationToken) =>
        {
            var result = await service.ConfirmUploadAsync(assignmentId, httpContext.User.GetUserId(), body, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequireLiveSession();

        // LRN-6/LRN-7: requirement-spec.md §6 "Student-owned". No permission string - the caller
        // must be an enrolled Student of this Assignment's CourseOffering, resolved through
        // Academic's own lookup inside the service.
        assignmentSubmissions.MapPost("/", async (Guid assignmentId, CreateSubmissionRequest body, HttpContext httpContext, SubmissionService service, CancellationToken cancellationToken) =>
        {
            var result = await service.SubmitAsync(assignmentId, httpContext.User.GetUserId(), body, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(
                dto => Results.Created($"/api/v1/learning/submissions/{dto.Id}", dto),
                error => error.ToProblemResult(httpContext));
        }).RequireLiveSession();

        // LRN-10: the Instructor's grading queue.
        assignmentSubmissions.MapGet("/", async (Guid assignmentId, bool? includeSuperseded, HttpContext httpContext, SubmissionQueryService service, CancellationToken cancellationToken) =>
        {
            var result = await service.ListForAssignmentAsync(assignmentId, httpContext.User.GetUserId(), includeSuperseded ?? false, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(LearningPermissions.SubmissionReadBatch);

        var submissions = group.MapGroup("/submissions");

        submissions.MapGet("/{id:guid}", async (Guid id, HttpContext httpContext, SubmissionQueryService service, CancellationToken cancellationToken) =>
        {
            var result = await service.GetByIdAsync(id, httpContext.User.GetUserId(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequireLiveSession();

        submissions.MapPost("/{id:guid}/evaluate", async (Guid id, EvaluateSubmissionRequest body, HttpContext httpContext, SubmissionService service, CancellationToken cancellationToken) =>
        {
            var result = await service.EvaluateAsync(id, httpContext.User.GetUserId(), body, httpContext.GetAuditContext(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(LearningPermissions.SubmissionEvaluate);

        submissions.MapGet("/{id:guid}/plagiarism-check", async (Guid id, HttpContext httpContext, PlagiarismCheckService service, CancellationToken cancellationToken) =>
        {
            var result = await service.GetForSubmissionAsync(id, httpContext.User.GetUserId(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(LearningPermissions.SubmissionEvaluate);

        submissions.MapPost("/{id:guid}/plagiarism-check/retry", async (Guid id, HttpContext httpContext, PlagiarismCheckService service, CancellationToken cancellationToken) =>
        {
            var result = await service.RetryAsync(id, httpContext.User.GetUserId(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(LearningPermissions.SubmissionEvaluate);
    }
}
