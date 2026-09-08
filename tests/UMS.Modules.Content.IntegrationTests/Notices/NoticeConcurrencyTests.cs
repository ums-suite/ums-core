using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Content.Application.Common;
using UMS.Modules.Content.Application.Notices;
using UMS.Modules.Content.Application.Scheduling;
using UMS.Modules.Content.Domain.Common;
using UMS.Modules.Content.IntegrationTests.Infrastructure;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Content.IntegrationTests.Notices;

/// <summary>
/// edge-cases.md "Two admins concurrently editing the same Notice/Banner (lost update)" and "A
/// scheduled publish_at firing while an admin is mid-edit" - genuine concurrent races (real
/// <see cref="Task.WhenAll"/>, each branch its own DI scope mirroring a distinct HTTP
/// request/worker tick), never a sequential retry, against the real `xmin`-backed optimistic
/// concurrency check (design-decisions.md "Concurrent-Edit Conflict Resolution": no writer -
/// human or scheduled job - is special-cased).
/// </summary>
[Collection(ContentApiTestCollectionDefinition.Name)]
public sealed class NoticeConcurrencyTests(ContentServiceFixture fixture)
{
    [Fact]
    public async Task Two_concurrent_edits_to_the_same_Draft_Notice_exactly_one_wins()
    {
        var actorA = Guid.NewGuid();
        var actorB = Guid.NewGuid();
        var (noticeId, version) = await CreateDraftAsync();

        async Task<Result<NoticeDto>> EditAsync(Guid actor, string title)
        {
            using var scope = fixture.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<NoticeService>();
            var audit = new AuditContext(actor, ActorIpAddress: null, Guid.NewGuid().ToString());
            return await service.EditContentAsync(noticeId, title, "Body", audit, version);
        }

        var results = await Task.WhenAll(EditAsync(actorA, "Title A"), EditAsync(actorB, "Title B"));

        Assert.Single(results, r => r.IsSuccess);
        Assert.Single(results, r => r.IsFailure && r.Error!.Code == "notice.concurrency_conflict");
    }

    [Fact]
    public async Task Scheduled_publish_job_racing_a_concurrent_admin_edit_exactly_one_wins()
    {
        var actor = Guid.NewGuid();
        var (noticeId, draftVersion) = await CreateDraftAsync();

        using (var setupScope = fixture.Services.CreateScope())
        {
            var service = setupScope.ServiceProvider.GetRequiredService<NoticeService>();
            var scheduled = await service.UpdateScheduleAsync(noticeId, DateTimeOffset.UtcNow.AddSeconds(-1), null, draftVersion);
            Assert.True(scheduled.IsSuccess);
            var schedule = await service.ScheduleAsync(noticeId, scheduled.Value.Version);
            Assert.True(schedule.IsSuccess);
        }

        uint scheduledVersion;
        using (var readScope = fixture.Services.CreateScope())
        {
            var service = readScope.ServiceProvider.GetRequiredService<NoticeService>();
            var current = await service.GetByIdAsync(noticeId, preferredLanguage: null, callerUserId: actor);
            scheduledVersion = current.Value.Version;
        }

        async Task<Result<NoticeDto>> PublishViaJobAsync()
        {
            using var scope = fixture.Services.CreateScope();
            var scheduling = scope.ServiceProvider.GetRequiredService<NoticeSchedulingService>();
            await scheduling.PublishDueAsync(batchSize: 10, correlationId: Guid.NewGuid().ToString());

            var service = scope.ServiceProvider.GetRequiredService<NoticeService>();
            return await service.GetByIdAsync(noticeId, preferredLanguage: null, callerUserId: actor);
        }

        async Task<Result<NoticeDto>> EditViaAdminAsync()
        {
            using var scope = fixture.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<NoticeService>();
            var audit = new AuditContext(actor, ActorIpAddress: null, Guid.NewGuid().ToString());
            return await service.EditContentAsync(noticeId, "Admin's last-second fix", "Body", audit, scheduledVersion);
        }

        // Genuinely concurrent - both branches race the SAME stale `scheduledVersion` snapshot
        // against the real xmin column, exactly as edge-cases.md frames it ("an Admin opens a
        // Scheduled Notice for a last-minute edit ... at the same moment the background publish
        // job ... transitions that same row").
        var editTask = EditViaAdminAsync();
        var jobTask = PublishViaJobAsync();
        var results = await Task.WhenAll(editTask, jobTask);
        var editResult = results[0];
        var jobRead = results[1];

        // design-decisions.md's own resolved posture: "the publish job's own transition never
        // blocks or waits on a lock ... it always proceeds and either wins or is itself the one
        // whose result an admin's later stale save conflicts against" - so exactly one of the two
        // outcomes below must hold, never both succeeding against the same stale version.
        Assert.True(jobRead.IsSuccess);
        Assert.True(
            (editResult.IsSuccess && jobRead.Value.Status == "Published" && editResult.Value.Title == "Admin's last-second fix")
            || (editResult.IsFailure && editResult.Error!.Code == "notice.concurrency_conflict" && jobRead.Value.Status == "Published"));
    }

    private async Task<(Guid NoticeId, uint Version)> CreateDraftAsync()
    {
        using var scope = fixture.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<NoticeService>();
        var created = await service.CreateAsync("Title", "Body", ContentAudience.Admin, null, isUrgent: false, Guid.NewGuid());
        Assert.True(created.IsSuccess);
        return (created.Value.Id, created.Value.Version);
    }
}
