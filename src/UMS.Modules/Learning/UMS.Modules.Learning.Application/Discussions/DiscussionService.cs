using System.Text.Json;
using UMS.Modules.Learning.Application.Abstractions;
using UMS.Modules.Learning.Application.Assignments;
using UMS.Modules.Learning.Application.Common;
using UMS.Modules.Learning.Domain.Discussions;
using UMS.Shared.Academic;
using UMS.Shared.Audit;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Learning.Application.Discussions;

/// <summary>
/// LRN-16/LRN-17/LRN-18: CourseOffering-scoped threads, posts/replies, and the Instructor's
/// moderation transition.
///
/// <para>
/// <b>Moderation writes to Audit synchronously, in the same transaction as the transition</b>
/// (requirement-spec.md §5 Auditability, §9.11) - the identical <c>IAuditRecorder</c> usage
/// Academic's and Faculty's own sensitive mutations already make, applied to a case
/// ums-requirements.md §4.1's original enumerated list did not anticipate. Removal is never a
/// delete: the post's content and authorship stay in this module's schema, with actor/reason/
/// timestamp on the entity itself.
/// </para>
/// </summary>
public sealed class DiscussionService(
    IDiscussionThreadRepository threads,
    ICourseOfferingLookup courseOfferings,
    IUnitOfWork unitOfWork,
    IAuditRecorder auditRecorder,
    IClock clock)
{
    public async Task<Result<DiscussionThreadDto>> CreateThreadAsync(
        Guid courseOfferingId,
        Guid callerUserId,
        CreateDiscussionThreadRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var access = await AssignmentQueryService.ResolveAccessAsync(courseOfferings, courseOfferingId, callerUserId, cancellationToken).ConfigureAwait(false);
        if (access.IsFailure)
        {
            return access.Error!;
        }

        var thread = DiscussionThread.Create(courseOfferingId, request.Title, callerUserId, clock.UtcNow);
        if (thread.IsFailure)
        {
            return thread.Error!;
        }

        if (!string.IsNullOrWhiteSpace(request.FirstPostBody))
        {
            var post = thread.Value.AddPost(callerUserId, request.FirstPostBody, null, clock.UtcNow);
            if (post.IsFailure)
            {
                return post.Error!;
            }
        }

        threads.Add(thread.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDto(thread.Value, access.Value.IsInstructor);
    }

    public async Task<Result<IReadOnlyList<DiscussionThreadDto>>> ListByCourseOfferingAsync(Guid courseOfferingId, Guid callerUserId, CancellationToken cancellationToken = default)
    {
        var access = await AssignmentQueryService.ResolveAccessAsync(courseOfferings, courseOfferingId, callerUserId, cancellationToken).ConfigureAwait(false);
        if (access.IsFailure)
        {
            return access.Error!;
        }

        var items = await threads.GetByCourseOfferingAsync(courseOfferingId, cancellationToken).ConfigureAwait(false);
        return items.Select(t => ToDto(t, access.Value.IsInstructor)).ToList();
    }

    public async Task<Result<DiscussionPostDto>> AddPostAsync(
        Guid threadId,
        Guid callerUserId,
        CreateDiscussionPostRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var thread = await threads.GetByIdAsync(new DiscussionThreadId(threadId), cancellationToken).ConfigureAwait(false);
        if (thread is null)
        {
            return Error.NotFound("discussion_thread.not_found", $"No DiscussionThread exists with id '{threadId}'.");
        }

        var access = await AssignmentQueryService.ResolveAccessAsync(courseOfferings, thread.CourseOfferingId, callerUserId, cancellationToken).ConfigureAwait(false);
        if (access.IsFailure)
        {
            return access.Error!;
        }

        var parentPostId = request.ParentPostId is { } parent ? new DiscussionPostId(parent) : (DiscussionPostId?)null;
        var post = thread.AddPost(callerUserId, request.Body, parentPostId, clock.UtcNow);
        if (post.IsFailure)
        {
            return post.Error!;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDto(post.Value, access.Value.IsInstructor);
    }

    /// <summary>LRN-18: remove or restore, gated on Instructor ownership of the offering and written to Audit synchronously. There is no Department-Head/Registrar review gate here, matching edge-cases.md's own residual note.</summary>
    public async Task<Result<DiscussionPostDto>> ModerateAsync(
        Guid postId,
        Guid callerUserId,
        ModerateDiscussionPostRequest request,
        AuditContext audit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(audit);

        var remove = request.Action switch
        {
            not null when string.Equals(request.Action, "remove", StringComparison.OrdinalIgnoreCase) => true,
            not null when string.Equals(request.Action, "restore", StringComparison.OrdinalIgnoreCase) => false,
            _ => (bool?)null,
        };

        if (remove is null)
        {
            return Error.Validation("discussion_post.invalid_moderation_action", $"'{request.Action}' is not a valid moderation action - use 'remove' or 'restore'.");
        }

        var thread = await threads.GetByPostIdAsync(new DiscussionPostId(postId), cancellationToken).ConfigureAwait(false);
        if (thread is null)
        {
            return Error.NotFound("discussion_post.not_found", $"No DiscussionPost exists with id '{postId}'.");
        }

        var isInstructor = await courseOfferings.IsInstructorForOfferingAsync(thread.CourseOfferingId, callerUserId, cancellationToken).ConfigureAwait(false);
        if (!isInstructor)
        {
            return Error.Forbidden("discussion_post.not_assigned_instructor", $"The calling user is not the assigned, active Instructor for CourseOffering '{thread.CourseOfferingId}'.");
        }

        var beforeStatus = thread.Posts.First(p => p.Id.Value == postId).ModerationStatus.ToString();
        var moderated = thread.Moderate(new DiscussionPostId(postId), remove.Value, callerUserId, request.Reason, clock.UtcNow);
        if (moderated.IsFailure)
        {
            return moderated.Error!;
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var auditRequest = audit.ToRequest(
            "DiscussionPost",
            postId.ToString(),
            remove.Value ? "moderate_remove" : "moderate_restore",
            JsonSerializer.Serialize(new { moderationStatus = beforeStatus }),
            JsonSerializer.Serialize(new { moderationStatus = moderated.Value.ModerationStatus.ToString() }),
            request.Reason);

        var commit = await TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, auditRequest, cancellationToken).ConfigureAwait(false);
        return commit.IsFailure ? commit.Error! : ToDto(moderated.Value, callerCanSeeRemovedContent: true);
    }

    internal static DiscussionThreadDto ToDto(DiscussionThread thread, bool callerCanSeeRemovedContent) => new(
        thread.Id.Value,
        thread.CourseOfferingId,
        thread.Title,
        thread.CreatedByUserId,
        thread.CreatedAt,
        thread.Posts.OrderBy(p => p.CreatedAt).Select(p => ToDto(p, callerCanSeeRemovedContent)).ToList());

    internal static DiscussionPostDto ToDto(DiscussionPost post, bool callerCanSeeRemovedContent) => new(
        post.Id.Value,
        post.DiscussionThreadId.Value,
        post.ParentPostId?.Value,
        post.AuthorUserId,
        post.ModerationStatus == ModerationStatus.Removed && !callerCanSeeRemovedContent ? null : post.Body,
        post.CreatedAt,
        post.ModerationStatus.ToString(),
        post.RemovedByUserId,
        post.RemovedReason,
        post.RemovedAt);
}
