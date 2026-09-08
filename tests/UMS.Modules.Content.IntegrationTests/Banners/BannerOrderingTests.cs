using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Content.Application.Banners;
using UMS.Modules.Content.Application.Common;
using UMS.Modules.Content.IntegrationTests.Infrastructure;

namespace UMS.Modules.Content.IntegrationTests.Banners;

/// <summary>
/// edge-cases.md "Two Banners' active windows overlap at the same sort_order": `sort_order`
/// ascending, ties broken by `created_at` ascending - the older Banner (among those tied on
/// priority) displays first, deterministically, against a real Postgres read (not just the
/// in-memory <see cref="UMS.Modules.Content.Domain.Common.ContentOrdering"/> unit test).
/// </summary>
[Collection(ContentApiTestCollectionDefinition.Name)]
public sealed class BannerOrderingTests(ContentServiceFixture fixture)
{
    [Fact]
    public async Task Two_Published_banners_tied_on_sort_order_are_ordered_by_created_at_ascending()
    {
        var actor = Guid.NewGuid();

        async Task<Guid> CreateAndPublishAsync(string headline)
        {
            using var scope = fixture.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<BannerService>();
            var created = await service.CreateAsync(headline, "https://cdn/banner.png", null, sortOrder: 1, actor);
            var scheduled = await service.UpdateScheduleAsync(created.Value.Id, DateTimeOffset.UtcNow.AddSeconds(-1), null, created.Value.Version);
            await service.ScheduleAsync(created.Value.Id, scheduled.Value.Version);
            var current = await service.GetByIdAsync(created.Value.Id);
            var audit = new AuditContext(actor, ActorIpAddress: null, Guid.NewGuid().ToString());
            var published = await service.PublishAsync(created.Value.Id, audit, current.Value.Version);
            Assert.True(published.IsSuccess);
            return created.Value.Id;
        }

        var olderId = await CreateAndPublishAsync("Older Banner");
        await Task.Delay(TimeSpan.FromMilliseconds(50));
        var newerId = await CreateAndPublishAsync("Newer Banner");

        using var readScope = fixture.Services.CreateScope();
        var reader = readScope.ServiceProvider.GetRequiredService<BannerService>();
        var active = await reader.ListActiveAsync();

        var olderIndex = active.ToList().FindIndex(b => b.Id == olderId);
        var newerIndex = active.ToList().FindIndex(b => b.Id == newerId);

        Assert.True(olderIndex >= 0 && newerIndex >= 0);
        Assert.True(olderIndex < newerIndex);
    }
}
