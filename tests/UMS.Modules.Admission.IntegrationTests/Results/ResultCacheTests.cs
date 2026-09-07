using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Admission.Application.Abstractions;
using UMS.Modules.Admission.IntegrationTests.Infrastructure;

namespace UMS.Modules.Admission.IntegrationTests.Results;

/// <summary>ADR-0007: the write-through cache - a real Redis (Testcontainers), not a fake, since the atomic multi-key Lua write and distributed lock are exactly what this suite verifies.</summary>
[Collection(AdmissionApiTestCollectionDefinition.Name)]
public sealed class ResultCacheTests(AdmissionServiceFixture fixture)
{
    [Fact]
    public async Task Writing_a_result_makes_it_readable_by_every_lookup_path()
    {
        using var scope = fixture.Services.CreateScope();
        var cache = scope.ServiceProvider.GetRequiredService<IResultCache>();

        var applicationNumber = $"APP-2026-{Guid.NewGuid():N}"[..20];
        var studentId = Guid.NewGuid();
        var examId = Guid.NewGuid();
        var rollNumber = $"R-{Guid.NewGuid():N}"[..10];
        var payload = "{\"outcome\":\"Admitted\"}";

        await cache.WriteResultAsync(new ResultCacheEntry(applicationNumber, studentId, examId, rollNumber, payload));

        Assert.Equal(payload, await cache.GetByApplicationNumberAsync(applicationNumber));
        Assert.Equal(payload, await cache.GetByStudentIdAsync(studentId));
        Assert.Equal(payload, await cache.GetByExamRollNumberAsync(examId, rollNumber));
    }

    [Fact]
    public async Task A_genuine_cache_miss_returns_null_never_a_stale_or_fabricated_value()
    {
        using var scope = fixture.Services.CreateScope();
        var cache = scope.ServiceProvider.GetRequiredService<IResultCache>();

        var result = await cache.GetByApplicationNumberAsync($"never-published-{Guid.NewGuid():N}");

        Assert.Null(result);
    }

    [Fact]
    public async Task A_correction_rewrite_atomically_replaces_all_three_keys()
    {
        using var scope = fixture.Services.CreateScope();
        var cache = scope.ServiceProvider.GetRequiredService<IResultCache>();

        var applicationNumber = $"APP-2026-{Guid.NewGuid():N}"[..20];
        var examId = Guid.NewGuid();
        var rollNumber = $"R-{Guid.NewGuid():N}"[..10];

        await cache.WriteResultAsync(new ResultCacheEntry(applicationNumber, null, examId, rollNumber, "{\"outcome\":\"Waitlisted\"}"));
        await cache.WriteResultAsync(new ResultCacheEntry(applicationNumber, null, examId, rollNumber, "{\"outcome\":\"Admitted\"}"));

        Assert.Equal("{\"outcome\":\"Admitted\"}", await cache.GetByApplicationNumberAsync(applicationNumber));
        Assert.Equal("{\"outcome\":\"Admitted\"}", await cache.GetByExamRollNumberAsync(examId, rollNumber));
    }

    [Fact]
    public async Task The_regeneration_lock_is_exclusive_until_released()
    {
        using var scope = fixture.Services.CreateScope();
        var cache = scope.ServiceProvider.GetRequiredService<IResultCache>();
        var campaignId = Guid.NewGuid();

        var firstLock = await cache.TryAcquireRegenerationLockAsync(campaignId, TimeSpan.FromSeconds(30));
        Assert.NotNull(firstLock);

        var secondLock = await cache.TryAcquireRegenerationLockAsync(campaignId, TimeSpan.FromSeconds(30));
        Assert.Null(secondLock);

        await firstLock!.DisposeAsync();

        var thirdLock = await cache.TryAcquireRegenerationLockAsync(campaignId, TimeSpan.FromSeconds(30));
        Assert.NotNull(thirdLock);
        await thirdLock!.DisposeAsync();
    }
}
