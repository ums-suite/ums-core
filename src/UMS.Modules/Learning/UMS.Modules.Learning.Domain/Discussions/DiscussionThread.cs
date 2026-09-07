using UMS.Modules.Learning.Domain.Common;
using UMS.Modules.Learning.Domain.Events;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Learning.Domain.Discussions;

/// <summary>
/// LRN-16/LRN-17/LRN-18: a <c>CourseOffering</c>-scoped forum thread, moderated by its Instructor
/// (docs/ddd/ubiquitous-language.md). Owns its <see cref="DiscussionPost"/>s - a post has no
/// lifecycle outside the thread it belongs to.
///
/// <para>
/// Enrollment/instructor scoping is enforced at the application layer against
/// <c>UMS.Shared.Academic.ICourseOfferingLookup</c> before any method here is reached - the
/// aggregate never reaches across modules itself.
/// </para>
/// </summary>
public sealed class DiscussionThread : AggregateRoot<DiscussionThreadId>
{
    private readonly List<DiscussionPost> _posts = [];

    private DiscussionThread()
    {
    }

    private DiscussionThread(DiscussionThreadId id, Guid courseOfferingId, string title, Guid createdByUserId, DateTimeOffset now)
    {
        Id = id;
        CourseOfferingId = courseOfferingId;
        Title = title;
        CreatedByUserId = createdByUserId;
        CreatedAt = now;
    }

    public Guid CourseOfferingId { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public Guid CreatedByUserId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public IReadOnlyCollection<DiscussionPost> Posts => _posts.AsReadOnly();

    public static Result<DiscussionThread> Create(Guid courseOfferingId, string title, Guid createdByUserId, DateTimeOffset now)
    {
        return string.IsNullOrWhiteSpace(title)
            ? Error.Validation("discussion_thread.title_required", "A DiscussionThread title is required.")
            : new DiscussionThread(DiscussionThreadId.New(), courseOfferingId, title.Trim(), createdByUserId, now);
    }

    /// <summary>LRN-17: adds a post or a reply. A reply's <paramref name="parentPostId"/> must name a post already in THIS thread - a cross-thread reply is a defect, not a feature.</summary>
    public Result<DiscussionPost> AddPost(Guid authorUserId, string body, DiscussionPostId? parentPostId, DateTimeOffset now)
    {
        if (parentPostId is { } parentId && _posts.TrueForAll(p => p.Id != parentId))
        {
            return Error.NotFound("discussion_post.parent_not_found", $"No DiscussionPost '{parentId}' exists in thread '{Id}'.");
        }

        var created = DiscussionPost.Create(Id, parentPostId, authorUserId, body, now);
        if (created.IsFailure)
        {
            return created.Error!;
        }

        _posts.Add(created.Value);
        Raise(new DiscussionPostCreated(created.Value.Id.Value, Id.Value, CourseOfferingId, authorUserId, parentPostId?.Value, now));
        return created.Value;
    }

    /// <summary>LRN-18: the Instructor's remove/restore transition. Content and authorship are always retained - see <see cref="DiscussionPost"/>'s own remarks.</summary>
    public Result<DiscussionPost> Moderate(DiscussionPostId postId, bool remove, Guid moderatorUserId, string? reason, DateTimeOffset now)
    {
        var post = _posts.Find(p => p.Id == postId);
        if (post is null)
        {
            return Error.NotFound("discussion_post.not_found", $"No DiscussionPost '{postId}' exists in thread '{Id}'.");
        }

        var previousStatus = post.ModerationStatus;
        var transition = remove
            ? post.Remove(moderatorUserId, reason ?? string.Empty, now)
            : post.Restore(moderatorUserId, now);

        if (transition.IsFailure)
        {
            return transition.Error!;
        }

        Raise(new DiscussionPostModerated(
            post.Id.Value,
            Id.Value,
            CourseOfferingId,
            previousStatus.ToString(),
            post.ModerationStatus.ToString(),
            moderatorUserId,
            post.RemovedReason,
            now));

        return post;
    }
}
