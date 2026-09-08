using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Content.Application.Common;
using UMS.Modules.Content.Application.Notices;
using UMS.Modules.Content.Domain.Common;
using UMS.Modules.Content.IntegrationTests.Infrastructure;

namespace UMS.Modules.Content.IntegrationTests.Notices;

/// <summary>
/// design-decisions.md "Cache-Correctness Backstop"/edge-cases.md "CDN/Redis cache for a
/// just-Archived notice is not invalidated in time": the origin read path (this fixture has no
/// real CDN - <see cref="NoticeService.GetByIdAsync"/> itself IS the origin) must unconditionally
/// report an Archived Notice as gone, independent of the (here, faked, always-succeeding) cache
/// invalidation attempt.
/// </summary>
[Collection(ContentApiTestCollectionDefinition.Name)]
public sealed class NoticeArchivedOriginBackstopTests(ContentServiceFixture fixture)
{
    [Fact]
    public async Task Manually_archived_notice_is_reported_as_notice_archived_by_the_origin_read_path()
    {
        var actor = Guid.NewGuid();
        Guid noticeId;
        uint version;
        using (var scope = fixture.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<NoticeService>();
            var created = await service.CreateAsync("Title", "Body", ContentAudience.Public, null, isUrgent: false, actor);
            noticeId = created.Value.Id;
            var translated = await service.UpsertTranslationAsync(noticeId, "bn", "শিরোনাম", "বিষয়বস্তু", actor, created.Value.Version);
            var audit = new AuditContext(actor, ActorIpAddress: null, Guid.NewGuid().ToString());
            var published = await service.PublishAsync(noticeId, audit, translated.Value.Version);
            version = published.Value.Version;
        }

        using (var scope = fixture.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<NoticeService>();
            var audit = new AuditContext(actor, ActorIpAddress: null, Guid.NewGuid().ToString());
            var archived = await service.ArchiveAsync(noticeId, audit, version);
            Assert.True(archived.IsSuccess);
        }

        using (var scope = fixture.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<NoticeService>();
            var result = await service.GetByIdAsync(noticeId, preferredLanguage: null, callerUserId: null);

            Assert.True(result.IsFailure);
            Assert.Equal("notice.archived", result.Error!.Code);
        }
    }
}
