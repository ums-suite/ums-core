using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Learning.Application.Discussions;
using UMS.Modules.Learning.Application.Permissions;
using UMS.Shared.Authorization;
using UMS.Shared.ErrorHandling;

namespace UMS.Modules.Learning.Api.Endpoints;

/// <summary>LRN-16/LRN-17/LRN-18: requirement-spec.md learning §6's DiscussionThread/DiscussionPost rows. Thread and post writes are enrollment-scoped (Student or Instructor); moderation additionally requires <c>learning.discussion.moderate</c> AND Instructor ownership of the offering.</summary>
internal static class DiscussionEndpoints
{
    public static void MapDiscussionEndpoints(this RouteGroupBuilder group)
    {
        var offeringThreads = group.MapGroup("/course-offerings/{courseOfferingId:guid}/discussion-threads");

        offeringThreads.MapPost("/", async (Guid courseOfferingId, CreateDiscussionThreadRequest body, HttpContext httpContext, DiscussionService service, CancellationToken cancellationToken) =>
        {
            var result = await service.CreateThreadAsync(courseOfferingId, httpContext.User.GetUserId(), body, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(
                dto => Results.Created($"/api/v1/learning/discussion-threads/{dto.Id}", dto),
                error => error.ToProblemResult(httpContext));
        }).RequireLiveSession();

        offeringThreads.MapGet("/", async (Guid courseOfferingId, HttpContext httpContext, DiscussionService service, CancellationToken cancellationToken) =>
        {
            var result = await service.ListByCourseOfferingAsync(courseOfferingId, httpContext.User.GetUserId(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequireLiveSession();

        group.MapPost("/discussion-threads/{threadId:guid}/posts", async (Guid threadId, CreateDiscussionPostRequest body, HttpContext httpContext, DiscussionService service, CancellationToken cancellationToken) =>
        {
            var result = await service.AddPostAsync(threadId, httpContext.User.GetUserId(), body, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(
                dto => Results.Created($"/api/v1/learning/discussion-threads/{threadId}/posts/{dto.Id}", dto),
                error => error.ToProblemResult(httpContext));
        }).RequireLiveSession();

        // LRN-18: remove/restore. A soft state transition plus a synchronous Audit write - never a
        // delete (requirement-spec.md §4, edge-cases.md's moderation entry).
        group.MapPost("/discussion-posts/{postId:guid}/moderate", async (Guid postId, ModerateDiscussionPostRequest body, HttpContext httpContext, DiscussionService service, CancellationToken cancellationToken) =>
        {
            var result = await service.ModerateAsync(postId, httpContext.User.GetUserId(), body, httpContext.GetAuditContext(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(LearningPermissions.DiscussionModerate);
    }
}
