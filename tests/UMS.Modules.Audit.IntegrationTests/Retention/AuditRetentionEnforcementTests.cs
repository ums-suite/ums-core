using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Audit.Application.Retention;
using UMS.Modules.Audit.Infrastructure.Persistence;
using UMS.Modules.Audit.IntegrationTests.Infrastructure;

namespace UMS.Modules.Audit.IntegrationTests.Retention;

/// <summary>
/// AUD-4: exercises the real Postgres DDL (pg_catalog partition listing, per-partition distinct
/// entity-type query, DETACH PARTITION) that the unit-tested pure decision logic
/// (RetentionPolicyEvaluatorTests) cannot itself verify - only an integration test against a real
/// partitioned table can confirm the raw SQL is actually valid.
///
/// <para>
/// Every test here that needs to detach a partition creates its own, well outside
/// AuditPartitionMaintenanceService's normal operating window (2020, versus "now" +/- a few
/// months) - this suite shares one Postgres container/collection with every other Audit
/// integration test class (AuditApiTestCollectionDefinition), so destructively detaching one of
/// the *real*, maintenance-managed partitions here would silently break
/// EntityHistoryPartitionBoundaryTests' own reliance on the previous-month partition still
/// existing, in a test-execution-order-dependent way.
/// </para>
/// </summary>
[Collection(AuditApiTestCollectionDefinition.Name)]
public class AuditRetentionEnforcementTests(AuditApiFixture fixture)
{
    [Fact]
    public async Task ListPartitionsAsync_reports_the_partitions_the_startup_maintenance_step_created()
    {
        using var scope = fixture.Services.CreateScope();
        var inspector = scope.ServiceProvider.GetRequiredService<IPartitionInspector>();

        var partitions = await inspector.ListPartitionsAsync();
        var names = partitions.Select(p => p.Name).ToHashSet();

        // AuditPartitionMaintenanceService.EnsureFuturePartitionsAsync runs at Host startup with
        // its default window (1 month behind, 3 months ahead, inclusive of the current month) -
        // this confirms the real Postgres catalog reports exactly that layout. Asserted as "at
        // least these five exist" rather than an exact total, since other tests in this shared
        // collection add and remove their own extra, isolated partitions.
        var expectedNames = UMS.Modules.Audit.Infrastructure.Partitioning.PartitionNaming
            .RequiredPartitions(DateTimeOffset.UtcNow, monthsBehind: 1, monthsAhead: 3)
            .Select(p => p.Name);

        Assert.All(expectedNames, expected => Assert.Contains(expected, names));
        Assert.All(partitions, p => Assert.True(p.RangeStart < p.RangeEndExclusive));
    }

    [Fact]
    public async Task A_fully_expired_partition_with_only_configured_entity_types_is_detached()
    {
        const string partitionName = "audit_log_entries_y2020m01";
        await CreateIsolatedPartitionAsync(partitionName, new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero));

        using var scope = fixture.Services.CreateScope();
        var inspector = scope.ServiceProvider.GetRequiredService<IPartitionInspector>();

        // No rows need to exist in the partition for a real DETACH to succeed - an empty
        // partition is trivially archivable (RetentionPolicyEvaluatorTests already covers this
        // decision in isolation; this confirms the DDL itself against a real, empty partition).
        var entityTypes = await inspector.GetDistinctEntityTypesAsync(partitionName);
        Assert.Empty(entityTypes);

        await inspector.DetachPartitionAsync(partitionName);

        var remaining = await inspector.ListPartitionsAsync();
        Assert.DoesNotContain(remaining, p => p.Name == partitionName);
    }

    [Fact]
    public async Task ArchiveEligiblePartitionsAsync_never_touches_the_current_or_future_partitions()
    {
        using var scope = fixture.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<AuditRetentionEnforcementService>();
        var inspector = scope.ServiceProvider.GetRequiredService<IPartitionInspector>();

        var before = (await inspector.ListPartitionsAsync()).Select(p => p.Name).ToHashSet();

        // Default configuration (no configured retention windows) means every entity type is
        // indefinite - nothing should ever be archived under this policy, current/past/future
        // alike (RetentionPolicyEvaluatorTests already proves the "indefinite by default" decision
        // in isolation; this confirms the orchestration honors it end to end, against whatever
        // real partitions exist at this point in the shared collection's run).
        var archived = await service.ArchiveEligiblePartitionsAsync();

        Assert.Empty(archived);

        var after = (await inspector.ListPartitionsAsync()).Select(p => p.Name).ToHashSet();
        Assert.Equal(before, after);
    }

    private async Task CreateIsolatedPartitionAsync(string partitionName, DateTimeOffset rangeStart)
    {
        var options = new DbContextOptionsBuilder<AuditDbContext>()
            .UseNpgsql(fixture.PostgresConnectionString)
            .Options;

        await using var context = new AuditDbContext(options);
        var rangeEnd = rangeStart.AddMonths(1);
        var sql = $"""
            CREATE TABLE IF NOT EXISTS audit.{partitionName}
            PARTITION OF audit.audit_log_entries
            FOR VALUES FROM ('{rangeStart:O}') TO ('{rangeEnd:O}');
            """;
        await context.Database.ExecuteSqlRawAsync(sql);
    }
}
