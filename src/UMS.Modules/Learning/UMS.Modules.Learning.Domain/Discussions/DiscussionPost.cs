using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Learning.Domain.Discussions;

/// <summary>
/// LRN-17/LRN-18 (module-local, pending glossary merge): one post or reply within a
/// <see cref="DiscussionThread"/>.
///
/// <para>
/// <b>Moderation is a reversible state transition, never a delete.</b> requirement-spec.md §4 and
/// edge-cases.md's "A Student posts something requiring moderation removal": a removed post keeps
/// its <see cref="Body"/> and <see cref="AuthorUserId"/> in this module's own schema - there is no
/// method on this type that clears either, and no hard-delete path anywhere in the module. The
/// actor, timestamp and reason live on the entity itself AND go to Audit synchronously at the
/// application layer, the exact field set ums-requirements.md §4.1 mandates platform-wide.
/// </para>
/// </summary>
public sealed class DiscussionPost
{
    private DiscussionPost()
    {
    }

    private DiscussionPost(DiscussionPostId id, DiscussionThreadId threadId, DiscussionPostId? parentPostId, Guid authorUserId, string body, DateTimeOffset now)
    {
        Id = id;
        DiscussionThreadId = threadId;
        ParentPostId = parentPostId;
        AuthorUserId = authorUserId;
        Body = body;
        ModerationStatus = ModerationStatus.Visible;
        CreatedAt = now;
    }

    public DiscussionPostId Id { get; private init; }

    public DiscussionThreadId DiscussionThreadId { get; private init; }

    /// <summary><see langword="null"/> for a top-level post; the post being replied to otherwise.</summary>
    public DiscussionPostId? ParentPostId { get; private init; }

    public Guid AuthorUserId { get; private init; }

    /// <summary>Instructor- and Student-authored freeform content. Never machine-translated (requirement-spec.md §5 Localization: only system chrome and structured metadata carry translations).</summary>
    public string Body { get; private init; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private init; }

    public ModerationStatus ModerationStatus { get; private set; }

    public Guid? RemovedByUserId { get; private set; }

    public string? RemovedReason { get; private set; }

    public DateTimeOffset? RemovedAt { get; private set; }

    public Guid? RestoredByUserId { get; private set; }

    public DateTimeOffset? RestoredAt { get; private set; }

    internal static Result<DiscussionPost> Create(DiscussionThreadId threadId, DiscussionPostId? parentPostId, Guid authorUserId, string body, DateTimeOffset now)
    {
        return string.IsNullOrWhiteSpace(body)
            ? Error.Validation("discussion_post.body_required", "A DiscussionPost body is required.")
            : new DiscussionPost(DiscussionPostId.New(), threadId, parentPostId, authorUserId, body.Trim(), now);
    }

    internal Result Remove(Guid removedByUserId, string reason, DateTimeOffset now)
    {
        if (ModerationStatus == ModerationStatus.Removed)
        {
            return Result.Failure(Error.Conflict("discussion_post.already_removed", "This DiscussionPost is already removed."));
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return Result.Failure(Error.Validation("discussion_post.removal_reason_required", "A moderation removal must record why - a bare status flag with no reason is not meaningfully different from an unexplained disappearance."));
        }

        ModerationStatus = ModerationStatus.Removed;
        RemovedByUserId = removedByUserId;
        RemovedReason = reason.Trim();
        RemovedAt = now;
        RestoredByUserId = null;
        RestoredAt = null;
        return Result.Success();
    }

    internal Result Restore(Guid restoredByUserId, DateTimeOffset now)
    {
        if (ModerationStatus == ModerationStatus.Visible)
        {
            return Result.Failure(Error.Conflict("discussion_post.not_removed", "This DiscussionPost is not removed and cannot be restored."));
        }

        ModerationStatus = ModerationStatus.Visible;
        RestoredByUserId = restoredByUserId;
        RestoredAt = now;
        return Result.Success();
    }
}
