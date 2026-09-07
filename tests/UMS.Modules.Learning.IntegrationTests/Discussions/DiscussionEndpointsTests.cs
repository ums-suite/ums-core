using System.Net;
using UMS.Modules.Learning.Application.Discussions;
using UMS.Modules.Learning.IntegrationTests.Infrastructure;

namespace UMS.Modules.Learning.IntegrationTests.Discussions;

/// <summary>LRN-16/LRN-17/LRN-18: enrollment-scoped threads and posts, plus the Instructor's moderation transition and its synchronous Audit write.</summary>
[Collection(LearningApiTestCollectionDefinition.Name)]
public sealed class DiscussionEndpointsTests(LearningApiFixture fixture)
{
    [Fact]
    public async Task An_enrolled_Student_can_open_a_thread_and_another_Student_can_reply()
    {
        var (client, context) = await SeedAsync();

        var thread = await CreateThreadAsync(client, context, "Does recursion always need a base case?");
        Assert.Single(thread.Posts);

        var reply = await client.PostAndReadAsync<DiscussionPostDto>(
            $"/api/v1/learning/discussion-threads/{thread.Id}/posts",
            context.SecondStudentToken,
            new CreateDiscussionPostRequest("Yes - otherwise it never terminates.", thread.Posts.First().Id));

        Assert.Equal(thread.Posts.First().Id, reply.ParentPostId);
        Assert.Equal("Visible", reply.ModerationStatus);
    }

    [Fact]
    public async Task A_Student_who_is_not_enrolled_can_neither_open_a_thread_nor_browse_them()
    {
        var (client, context) = await SeedAsync();
        await CreateThreadAsync(client, context, "Week 3 questions");
        var outsiderToken = await (await ScenarioAsync()).UnenrolledStudentTokenAsync();

        var create = await client.PostAsync(
            $"/api/v1/learning/course-offerings/{context.CourseOfferingId}/discussion-threads",
            outsiderToken,
            new CreateDiscussionThreadRequest("Not my course", null));
        var browse = await client.GetAsync(
            $"/api/v1/learning/course-offerings/{context.CourseOfferingId}/discussion-threads",
            outsiderToken);

        await create.AssertProblemCodeAsync(HttpStatusCode.Forbidden, "learning.not_enrolled_or_instructor");
        await browse.AssertProblemCodeAsync(HttpStatusCode.Forbidden, "learning.not_enrolled_or_instructor");
    }

    [Fact]
    public async Task A_reply_naming_a_post_from_another_thread_is_rejected()
    {
        var (client, context) = await SeedAsync();
        var first = await CreateThreadAsync(client, context, "Thread one");
        var second = await CreateThreadAsync(client, context, "Thread two");

        var response = await client.PostAsync(
            $"/api/v1/learning/discussion-threads/{second.Id}/posts",
            context.StudentToken,
            new CreateDiscussionPostRequest("Cross-thread reply.", first.Posts.First().Id));

        await response.AssertProblemCodeAsync(HttpStatusCode.NotFound, "discussion_post.parent_not_found");
    }

    /// <summary>
    /// edge-cases.md "A Student posts something requiring moderation removal": a state transition,
    /// not a delete - the post row survives with its author and body retained, a reason recorded,
    /// and a synchronous Audit entry written in the same transaction. Ordinary readers see the
    /// removal but not the content.
    /// </summary>
    [Fact]
    public async Task Moderation_removes_a_post_as_a_state_transition_retaining_content_and_writing_Audit()
    {
        var (client, context) = await SeedAsync();
        var thread = await CreateThreadAsync(client, context, "Week 3 questions", "Selling last year's solutions, DM me.");
        var postId = thread.Posts.First().Id;

        var moderated = await client.PostAndReadAsync<DiscussionPostDto>(
            $"/api/v1/learning/discussion-posts/{postId}/moderate",
            context.InstructorToken,
            new ModerateDiscussionPostRequest("remove", "Academic-dishonesty solicitation."));

        Assert.Equal("Removed", moderated.ModerationStatus);
        Assert.Equal(context.InstructorUserId, moderated.RemovedByUserId);
        Assert.Equal("Academic-dishonesty solicitation.", moderated.RemovedReason);
        Assert.NotNull(moderated.RemovedAt);
        Assert.Equal(1, await AuditEntryCounter.CountAsync(fixture, "DiscussionPost", postId.ToString(), "moderate_remove"));

        // The row is retained, not deleted - it still comes back on a browse, flagged Removed.
        var studentView = await client.GetAndReadAsync<List<DiscussionThreadDto>>(
            $"/api/v1/learning/course-offerings/{context.CourseOfferingId}/discussion-threads",
            context.StudentToken);
        var seenByStudent = studentView.Single(t => t.Id == thread.Id).Posts.Single(p => p.Id == postId);
        Assert.Equal("Removed", seenByStudent.ModerationStatus);
        Assert.Null(seenByStudent.Body);

        // The Instructor, who moderates, still sees the retained content.
        var instructorView = await client.GetAndReadAsync<List<DiscussionThreadDto>>(
            $"/api/v1/learning/course-offerings/{context.CourseOfferingId}/discussion-threads",
            context.InstructorToken);
        var seenByInstructor = instructorView.Single(t => t.Id == thread.Id).Posts.Single(p => p.Id == postId);
        Assert.Equal("Selling last year's solutions, DM me.", seenByInstructor.Body);
    }

    [Fact]
    public async Task A_removal_is_reversible_and_the_restore_is_itself_audited()
    {
        var (client, context) = await SeedAsync();
        var thread = await CreateThreadAsync(client, context, "Borderline", "Borderline content.");
        var postId = thread.Posts.First().Id;
        await client.PostAndReadAsync<DiscussionPostDto>(
            $"/api/v1/learning/discussion-posts/{postId}/moderate",
            context.InstructorToken,
            new ModerateDiscussionPostRequest("remove", "Reviewed on report."));

        var restored = await client.PostAndReadAsync<DiscussionPostDto>(
            $"/api/v1/learning/discussion-posts/{postId}/moderate",
            context.InstructorToken,
            new ModerateDiscussionPostRequest("restore", null));

        Assert.Equal("Visible", restored.ModerationStatus);
        Assert.Equal(1, await AuditEntryCounter.CountAsync(fixture, "DiscussionPost", postId.ToString(), "moderate_restore"));
    }

    [Fact]
    public async Task A_removal_without_a_reason_is_rejected()
    {
        var (client, context) = await SeedAsync();
        var thread = await CreateThreadAsync(client, context, "Spam thread", "Spam.");

        var response = await client.PostAsync(
            $"/api/v1/learning/discussion-posts/{thread.Posts.First().Id}/moderate",
            context.InstructorToken,
            new ModerateDiscussionPostRequest("remove", "   "));

        await response.AssertProblemCodeAsync(HttpStatusCode.BadRequest, "discussion_post.removal_reason_required");
    }

    [Fact]
    public async Task A_Student_cannot_moderate_a_post_even_their_own()
    {
        var (client, context) = await SeedAsync();
        var thread = await CreateThreadAsync(client, context, "Mine", "My own post.");

        var response = await client.PostAsync(
            $"/api/v1/learning/discussion-posts/{thread.Posts.First().Id}/moderate",
            context.StudentToken,
            new ModerateDiscussionPostRequest("remove", "Changed my mind."));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task An_Instructor_who_does_not_own_the_offering_cannot_moderate_its_posts()
    {
        var (client, context) = await SeedAsync();
        var otherInstructorToken = await (await ScenarioAsync()).UnrelatedInstructorTokenAsync();
        var thread = await CreateThreadAsync(client, context, "Not theirs", "Content.");

        var response = await client.PostAsync(
            $"/api/v1/learning/discussion-posts/{thread.Posts.First().Id}/moderate",
            otherInstructorToken,
            new ModerateDiscussionPostRequest("remove", "Not my course."));

        await response.AssertProblemCodeAsync(HttpStatusCode.Forbidden, "discussion_post.not_assigned_instructor");
    }

    [Fact]
    public async Task An_unknown_moderation_action_is_rejected()
    {
        var (client, context) = await SeedAsync();
        var thread = await CreateThreadAsync(client, context, "Thread", "Content.");

        var response = await client.PostAsync(
            $"/api/v1/learning/discussion-posts/{thread.Posts.First().Id}/moderate",
            context.InstructorToken,
            new ModerateDiscussionPostRequest("obliterate", "Nope."));

        await response.AssertProblemCodeAsync(HttpStatusCode.BadRequest, "discussion_post.invalid_moderation_action");
    }

    private static Task<DiscussionThreadDto> CreateThreadAsync(
        HttpClient client,
        LearningTestDataSeeder.CourseContext context,
        string title,
        string firstPostBody = "Opening post.") =>
        client.PostAndReadAsync<DiscussionThreadDto>(
            $"/api/v1/learning/course-offerings/{context.CourseOfferingId}/discussion-threads",
            context.StudentToken,
            new CreateDiscussionThreadRequest(title, firstPostBody));

    /// <summary>One course context per test class, memoized - see <see cref="LearningScenario"/> for why this is not seeded per test.</summary>
    private async Task<(HttpClient Client, LearningTestDataSeeder.CourseContext Context)> SeedAsync()
    {
        var scenario = await LearningScenario.GetAsync(fixture, "discussions");
        return (scenario.Client, scenario.Context);
    }

    private Task<LearningScenario> ScenarioAsync() => LearningScenario.GetAsync(fixture, "discussions");
}
