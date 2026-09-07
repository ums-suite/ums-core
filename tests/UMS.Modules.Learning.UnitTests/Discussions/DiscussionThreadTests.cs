using UMS.Modules.Learning.Domain.Discussions;
using UMS.Modules.Learning.Domain.Events;

namespace UMS.Modules.Learning.UnitTests.Discussions;

/// <summary>LRN-16/LRN-17/LRN-18: threads, posts/replies, and the moderation state transition that is never a delete (requirement-spec.md learning §4; edge-cases.md's moderation entry).</summary>
public sealed class DiscussionThreadTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 5, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_thread_without_a_title_is_rejected()
    {
        var result = DiscussionThread.Create(Guid.NewGuid(), "   ", Guid.NewGuid(), Now);

        Assert.True(result.IsFailure);
        Assert.Equal("discussion_thread.title_required", result.Error!.Code);
    }

    [Fact]
    public void Adding_a_post_raises_DiscussionPostCreated()
    {
        var thread = Create();

        var post = thread.AddPost(Guid.NewGuid(), "Does recursion always need a base case?", null, Now);

        Assert.True(post.IsSuccess);
        Assert.Equal(ModerationStatus.Visible, post.Value.ModerationStatus);
        Assert.Single(thread.DomainEvents.OfType<DiscussionPostCreated>());
    }

    [Fact]
    public void A_post_with_an_empty_body_is_rejected()
    {
        var thread = Create();

        var post = thread.AddPost(Guid.NewGuid(), "   ", null, Now);

        Assert.True(post.IsFailure);
        Assert.Equal("discussion_post.body_required", post.Error!.Code);
    }

    [Fact]
    public void A_reply_records_its_parent_post()
    {
        var thread = Create();
        var parent = thread.AddPost(Guid.NewGuid(), "Question?", null, Now).Value;

        var reply = thread.AddPost(Guid.NewGuid(), "Answer.", parent.Id, Now.AddMinutes(1));

        Assert.True(reply.IsSuccess);
        Assert.Equal(parent.Id, reply.Value.ParentPostId);
    }

    /// <summary>A reply must name a post in THIS thread - a cross-thread reply is a defect, not a feature.</summary>
    [Fact]
    public void A_reply_to_a_post_that_is_not_in_this_thread_is_rejected()
    {
        var thread = Create();

        var reply = thread.AddPost(Guid.NewGuid(), "Answer.", DiscussionPostId.New(), Now);

        Assert.True(reply.IsFailure);
        Assert.Equal("discussion_post.parent_not_found", reply.Error!.Code);
    }

    /// <summary>edge-cases.md: soft removal - content and authorship are retained, never hard-deleted.</summary>
    [Fact]
    public void Removing_a_post_retains_its_content_and_authorship_and_records_who_and_why()
    {
        var thread = Create();
        var author = Guid.NewGuid();
        var post = thread.AddPost(author, "Selling last year's solutions, DM me.", null, Now).Value;
        var moderator = Guid.NewGuid();
        thread.ClearDomainEvents();

        var moderated = thread.Moderate(post.Id, remove: true, moderator, "Academic-dishonesty solicitation.", Now.AddHours(1));

        Assert.True(moderated.IsSuccess);
        Assert.Equal(ModerationStatus.Removed, post.ModerationStatus);
        Assert.Equal("Selling last year's solutions, DM me.", post.Body);
        Assert.Equal(author, post.AuthorUserId);
        Assert.Equal(moderator, post.RemovedByUserId);
        Assert.Equal("Academic-dishonesty solicitation.", post.RemovedReason);
        Assert.Equal(Now.AddHours(1), post.RemovedAt);
        Assert.Single(thread.Posts);
    }

    [Fact]
    public void Moderation_raises_DiscussionPostModerated_with_the_before_and_after_status()
    {
        var thread = Create();
        var post = thread.AddPost(Guid.NewGuid(), "Off-topic spam.", null, Now).Value;
        thread.ClearDomainEvents();

        thread.Moderate(post.Id, remove: true, Guid.NewGuid(), "Spam.", Now);

        var moderated = Assert.Single(thread.DomainEvents.OfType<DiscussionPostModerated>());
        Assert.Equal("Visible", moderated.PreviousModerationStatus);
        Assert.Equal("Removed", moderated.NewModerationStatus);
        Assert.Equal("Spam.", moderated.Reason);
    }

    [Fact]
    public void A_removal_without_a_reason_is_rejected()
    {
        var thread = Create();
        var post = thread.AddPost(Guid.NewGuid(), "Something.", null, Now).Value;

        var moderated = thread.Moderate(post.Id, remove: true, Guid.NewGuid(), "  ", Now);

        Assert.True(moderated.IsFailure);
        Assert.Equal("discussion_post.removal_reason_required", moderated.Error!.Code);
    }

    /// <summary>edge-cases.md: removal is reversible - restore is itself an audited transition.</summary>
    [Fact]
    public void A_removed_post_can_be_restored_and_the_restore_is_itself_recorded()
    {
        var thread = Create();
        var post = thread.AddPost(Guid.NewGuid(), "Borderline.", null, Now).Value;
        thread.Moderate(post.Id, remove: true, Guid.NewGuid(), "Reviewed on report.", Now);
        thread.ClearDomainEvents();
        var restorer = Guid.NewGuid();

        var restored = thread.Moderate(post.Id, remove: false, restorer, null, Now.AddHours(2));

        Assert.True(restored.IsSuccess);
        Assert.Equal(ModerationStatus.Visible, post.ModerationStatus);
        Assert.Equal(restorer, post.RestoredByUserId);
        Assert.Equal(Now.AddHours(2), post.RestoredAt);
        var moderated = Assert.Single(thread.DomainEvents.OfType<DiscussionPostModerated>());
        Assert.Equal("Removed", moderated.PreviousModerationStatus);
        Assert.Equal("Visible", moderated.NewModerationStatus);
    }

    [Fact]
    public void Removing_an_already_removed_post_is_rejected()
    {
        var thread = Create();
        var post = thread.AddPost(Guid.NewGuid(), "Spam.", null, Now).Value;
        thread.Moderate(post.Id, remove: true, Guid.NewGuid(), "Spam.", Now);

        var again = thread.Moderate(post.Id, remove: true, Guid.NewGuid(), "Spam again.", Now);

        Assert.True(again.IsFailure);
        Assert.Equal("discussion_post.already_removed", again.Error!.Code);
    }

    [Fact]
    public void Restoring_a_post_that_was_never_removed_is_rejected()
    {
        var thread = Create();
        var post = thread.AddPost(Guid.NewGuid(), "Fine.", null, Now).Value;

        var restored = thread.Moderate(post.Id, remove: false, Guid.NewGuid(), null, Now);

        Assert.True(restored.IsFailure);
        Assert.Equal("discussion_post.not_removed", restored.Error!.Code);
    }

    [Fact]
    public void Moderating_a_post_that_is_not_in_this_thread_is_rejected()
    {
        var thread = Create();

        var moderated = thread.Moderate(DiscussionPostId.New(), remove: true, Guid.NewGuid(), "Spam.", Now);

        Assert.True(moderated.IsFailure);
        Assert.Equal("discussion_post.not_found", moderated.Error!.Code);
    }

    private static DiscussionThread Create() =>
        DiscussionThread.Create(Guid.NewGuid(), "Week 3 questions", Guid.NewGuid(), Now).Value;
}
