namespace UMS.Modules.Learning.Application.Discussions;

public sealed record DiscussionThreadDto(
    Guid Id,
    Guid CourseOfferingId,
    string Title,
    Guid CreatedByUserId,
    DateTimeOffset CreatedAt,
    IReadOnlyCollection<DiscussionPostDto> Posts);

/// <summary>
/// A removed post is returned with <c>ModerationStatus = "Removed"</c> and a <see langword="null"/>
/// <paramref name="Body"/> for ordinary readers, while an Instructor sees the retained content -
/// the post itself is never hard-deleted from this module's schema (requirement-spec.md §4,
/// edge-cases.md's moderation entry).
/// </summary>
public sealed record DiscussionPostDto(
    Guid Id,
    Guid DiscussionThreadId,
    Guid? ParentPostId,
    Guid AuthorUserId,
    string? Body,
    DateTimeOffset CreatedAt,
    string ModerationStatus,
    Guid? RemovedByUserId,
    string? RemovedReason,
    DateTimeOffset? RemovedAt);

public sealed record CreateDiscussionThreadRequest(string Title, string? FirstPostBody);

public sealed record CreateDiscussionPostRequest(string Body, Guid? ParentPostId);

/// <summary><paramref name="Action"/> is <c>"remove"</c> or <c>"restore"</c>; <paramref name="Reason"/> is required for a removal.</summary>
public sealed record ModerateDiscussionPostRequest(string Action, string? Reason);
