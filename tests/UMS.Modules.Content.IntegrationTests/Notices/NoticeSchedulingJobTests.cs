using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Content.Application.Notices;
using UMS.Modules.Content.Application.Scheduling;
using UMS.Modules.Content.Domain.Common;
using UMS.Modules.Content.Infrastructure.Persistence;
using UMS.Modules.Content.IntegrationTests.Infrastructure;

namespace UMS.Modules.Content.IntegrationTests.Notices;

/// <summary>
/// CNT-4: the `publish_at &lt;= now AND status = Scheduled` / `expire_at &lt;= now AND status =
/// Published` scan - requirement-spec.md §4/§8; edge-cases.md "A Notice's translation exists in
/// only one language when publish_at fires."
/// </summary>
[Collection(ContentApiTestCollectionDefinition.Name)]
public sealed class NoticeSchedulingJobTests(ContentServiceFixture fixture)
{
    [Fact]
    public async Task PublishDueAsync_publishes_an_Admin_only_Scheduled_notice_whose_publish_at_has_passed()
    {
        Guid noticeId;
        using (var scope = fixture.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<NoticeService>();
            var created = await service.CreateAsync("Title", "Body", ContentAudience.Admin, null, isUrgent: false, Guid.NewGuid());
            noticeId = created.Value.Id;
            var scheduled = await service.UpdateScheduleAsync(noticeId, DateTimeOffset.UtcNow.AddSeconds(-5), null, created.Value.Version);
            await service.ScheduleAsync(noticeId, scheduled.Value.Version);
        }

        using (var scope = fixture.Services.CreateScope())
        {
            var scheduling = scope.ServiceProvider.GetRequiredService<NoticeSchedulingService>();
            var publishedCount = await scheduling.PublishDueAsync(batchSize: 50, correlationId: Guid.NewGuid().ToString());
            Assert.True(publishedCount >= 1);
        }

        using (var scope = fixture.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<NoticeService>();
            var result = await service.GetByIdAsync(noticeId, preferredLanguage: null, callerUserId: Guid.NewGuid());
            Assert.Equal("Published", result.Value.Status);
        }
    }

    [Fact]
    public async Task ExpireDueAsync_archives_a_Published_notice_past_its_expire_at()
    {
        Guid noticeId;
        using (var scope = fixture.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<NoticeService>();
            var created = await service.CreateAsync("Title", "Body", ContentAudience.Admin, null, isUrgent: false, Guid.NewGuid());
            noticeId = created.Value.Id;
            var scheduled = await service.UpdateScheduleAsync(noticeId, DateTimeOffset.UtcNow.AddSeconds(-10), DateTimeOffset.UtcNow.AddSeconds(-1), created.Value.Version);
            await service.ScheduleAsync(noticeId, scheduled.Value.Version);
        }

        using (var scope = fixture.Services.CreateScope())
        {
            var scheduling = scope.ServiceProvider.GetRequiredService<NoticeSchedulingService>();
            await scheduling.PublishDueAsync(batchSize: 50, correlationId: Guid.NewGuid().ToString());
            var archivedCount = await scheduling.ExpireDueAsync(batchSize: 50, correlationId: Guid.NewGuid().ToString());
            Assert.True(archivedCount >= 1);
        }

        using (var scope = fixture.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<NoticeService>();
            var result = await service.GetByIdAsync(noticeId, preferredLanguage: null, callerUserId: Guid.NewGuid());
            Assert.True(result.IsFailure);
            Assert.Equal("notice.archived", result.Error!.Code);
        }
    }

    /// <summary>
    /// edge-cases.md "A Notice's translation exists in only one language when publish_at fires":
    /// a genuine translation-row deletion via raw SQL AFTER scheduling (the only way to reach this
    /// state at all - see NoticeTests' own unit-test remarks on why the aggregate's public API
    /// can't construct it directly), then the job's own defensive re-check must leave the row in
    /// Scheduled rather than force-publish it incomplete.
    /// </summary>
    [Fact]
    public async Task PublishDueAsync_leaves_a_Public_notice_Scheduled_when_its_translation_disappears_after_scheduling()
    {
        Guid noticeId;
        using (var scope = fixture.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<NoticeService>();
            var created = await service.CreateAsync("Title", "Body", ContentAudience.Public, null, isUrgent: false, Guid.NewGuid());
            noticeId = created.Value.Id;
            var translated = await service.UpsertTranslationAsync(noticeId, "bn", "শিরোনাম", "বিষয়বস্তু", Guid.NewGuid(), created.Value.Version);
            var scheduled = await service.UpdateScheduleAsync(noticeId, DateTimeOffset.UtcNow.AddSeconds(-5), null, translated.Value.Version);
            var schedule = await service.ScheduleAsync(noticeId, scheduled.Value.Version);
            Assert.True(schedule.IsSuccess);
        }

        // Simulate "a translator's edit correction that temporarily blanks a field" (edge-cases.md)
        // by deleting the translation row directly - bypassing the aggregate's own API entirely,
        // since it has no removal method.
        using (var scope = fixture.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ContentDbContext>();
            await dbContext.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM content.notice_translations WHERE notice_id = {noticeId}");
        }

        using (var scope = fixture.Services.CreateScope())
        {
            var scheduling = scope.ServiceProvider.GetRequiredService<NoticeSchedulingService>();
            await scheduling.PublishDueAsync(batchSize: 50, correlationId: Guid.NewGuid().ToString());
        }

        using (var scope = fixture.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<NoticeService>();
            var result = await service.GetByIdAsync(noticeId, preferredLanguage: null, callerUserId: Guid.NewGuid());
            Assert.True(result.IsSuccess);
            Assert.Equal("Scheduled", result.Value.Status);
        }
    }
}
