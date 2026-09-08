using UMS.Modules.Content.Domain.Common;
using UMS.Modules.Content.Domain.Events;
using UMS.Modules.Content.Domain.Notices;

namespace UMS.Modules.Content.UnitTests.Notices;

/// <summary>
/// CNT-1/2/15: requirement-spec.md §3/§4; design-decisions.md "Bilingual-Completeness Gate
/// Enforcement Point" and "Concurrent-Edit Conflict Resolution." These tests cover only the
/// in-memory state machine/invariants - the `xmin` optimistic-concurrency check itself is an
/// Infrastructure/EF concern, exercised by the integration test suite's genuine concurrent-write
/// races instead.
/// </summary>
public sealed class NoticeTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly Guid Actor = Guid.NewGuid();

    private static Notice Draft(ContentAudience audience = ContentAudience.Admin) =>
        Notice.Create("Title", "Body", audience, organizationNodeId: null, isUrgent: false, Actor, Now).Value;

    [Fact]
    public void Create_requires_at_least_one_audience()
    {
        var result = Notice.Create("Title", "Body", ContentAudience.None, null, false, Actor, Now);

        Assert.True(result.IsFailure);
        Assert.Equal("notice.audience_required", result.Error!.Code);
    }

    [Fact]
    public void Create_requires_title_and_body()
    {
        Assert.Equal("notice.title_required", Notice.Create(" ", "Body", ContentAudience.Admin, null, false, Actor, Now).Error!.Code);
        Assert.Equal("notice.body_required", Notice.Create("Title", " ", ContentAudience.Admin, null, false, Actor, Now).Error!.Code);
    }

    // --- Bilingual-completeness gate: call site #1, Draft -> Scheduled (Notice.Schedule) ---

    [Fact]
    public void Schedule_of_Public_notice_with_only_English_is_rejected()
    {
        var notice = Draft(ContentAudience.Public);
        notice.UpdateSchedule(Now.AddDays(1), null, Now);

        var result = notice.Schedule(Now);

        Assert.True(result.IsFailure);
        Assert.Equal("notice.bilingual_incomplete", result.Error!.Code);
        Assert.Equal(SchedulableStatus.Draft, notice.Status);
    }

    [Fact]
    public void Schedule_of_Public_notice_with_English_and_Bengali_succeeds()
    {
        var notice = Draft(ContentAudience.Public);
        notice.UpsertTranslation("bn", "শিরোনাম", "বিষয়বস্তু", Actor, Now);
        notice.UpdateSchedule(Now.AddDays(1), null, Now);

        var result = notice.Schedule(Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(SchedulableStatus.Scheduled, notice.Status);
    }

    [Fact]
    public void Schedule_of_Admin_only_notice_with_only_English_is_exempt_and_succeeds()
    {
        var notice = Draft(ContentAudience.Admin);
        notice.UpdateSchedule(Now.AddDays(1), null, Now);

        var result = notice.Schedule(Now);

        Assert.True(result.IsSuccess);
    }

    // --- Bilingual-completeness gate: call sites #2 (manual /publish) and #3 (the scheduled job's
    // own commit) both go through the SAME Notice.Publish method - see its own remarks. ---

    [Fact]
    public void Publish_of_Public_notice_with_only_English_is_rejected_whether_called_manually_or_by_the_job()
    {
        var notice = Draft(ContentAudience.Public);

        var result = notice.Publish(Now);

        Assert.True(result.IsFailure);
        Assert.Equal("notice.bilingual_incomplete", result.Error!.Code);
        Assert.Equal(SchedulableStatus.Draft, notice.Status);
        Assert.DoesNotContain(notice.DomainEvents, e => e is NoticePublished);
    }

    [Fact]
    public void Publish_of_Public_notice_with_both_languages_succeeds_and_raises_NoticePublished()
    {
        var notice = Draft(ContentAudience.Public);
        notice.UpsertTranslation("bn", "শিরোনাম", "বিষয়বস্তু", Actor, Now);

        var result = notice.Publish(Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(SchedulableStatus.Published, notice.Status);
        Assert.Contains(notice.DomainEvents, e => e is NoticePublished);
    }

    [Fact]
    public void Publish_of_a_Scheduled_Public_notice_with_both_languages_present_succeeds()
    {
        // The genuine "translation went missing between scheduling and firing" scenario
        // (edge-cases.md) requires a translation row to be deleted out from under an
        // already-Scheduled Notice - not expressible via this aggregate's own public API (no
        // translation-removal method exists), so it is covered at the integration-test level
        // instead (a raw SQL delete against notice_translations, then the job's own
        // PublishDueAsync). This test instead confirms the ordinary, still-complete case: the
        // job's own Publish() call (identical to the endpoint's) succeeds when scheduling-time
        // completeness genuinely still holds at publish time.
        var notice = Draft(ContentAudience.Public);
        notice.UpsertTranslation("bn", "শিরোনাম", "বিষয়বস্তু", Actor, Now);
        notice.UpdateSchedule(Now.AddDays(1), null, Now);
        Assert.True(notice.Schedule(Now).IsSuccess);

        var result = notice.Publish(Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(SchedulableStatus.Published, notice.Status);
    }

    // --- Legal state-transition coverage ---

    [Fact]
    public void Full_lifecycle_Draft_Scheduled_Published_Archived_succeeds_in_order()
    {
        var notice = Draft();
        notice.UpdateSchedule(Now.AddDays(1), null, Now);

        Assert.True(notice.Schedule(Now).IsSuccess);
        Assert.True(notice.Publish(Now).IsSuccess);
        Assert.True(notice.Archive(Now).IsSuccess);
        Assert.Equal(SchedulableStatus.Archived, notice.Status);
        Assert.Contains(notice.DomainEvents, e => e is NoticeArchived);
    }

    [Fact]
    public void Archive_from_Draft_is_rejected()
    {
        var notice = Draft();

        var result = notice.Archive(Now);

        Assert.True(result.IsFailure);
        Assert.Equal("notice.not_published", result.Error!.Code);
    }

    [Fact]
    public void Schedule_from_Published_is_rejected()
    {
        var notice = Draft();
        notice.Publish(Now);

        var result = notice.Schedule(Now);

        Assert.True(result.IsFailure);
        Assert.Equal("notice.not_draft", result.Error!.Code);
    }

    [Fact]
    public void EditContent_after_Archived_is_rejected()
    {
        var notice = Draft();
        notice.Publish(Now);
        notice.Archive(Now);

        var result = notice.EditContent("New Title", "New Body", Actor, Now);

        Assert.True(result.IsFailure);
        Assert.Equal("notice.archived", result.Error!.Code);
    }

    [Fact]
    public void UpsertTranslation_rejects_English_as_a_translation_language()
    {
        var notice = Draft();

        var result = notice.UpsertTranslation("en", "Title", "Body", Actor, Now);

        Assert.True(result.IsFailure);
        Assert.Equal("notice.translation_english_not_allowed", result.Error!.Code);
    }

    [Fact]
    public void UpsertTranslation_twice_for_the_same_language_updates_in_place_rather_than_duplicating()
    {
        var notice = Draft();

        notice.UpsertTranslation("bn", "First", "Body1", Actor, Now);
        notice.UpsertTranslation("bn", "Second", "Body2", Actor, Now);

        Assert.Single(notice.Translations);
        Assert.Equal("Second", notice.Translations.Single().Title);
    }

    [Fact]
    public void UpdateSchedule_rejects_expireAt_not_strictly_after_publishAt()
    {
        var notice = Draft();

        var result = notice.UpdateSchedule(Now, Now, Now);

        Assert.True(result.IsFailure);
        Assert.Equal("notice.invalid_window", result.Error!.Code);
    }
}
