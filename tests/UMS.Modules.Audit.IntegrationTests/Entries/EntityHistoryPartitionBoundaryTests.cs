using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Audit.Application.Entries;
using UMS.Modules.Audit.Domain.Entries;
using UMS.Modules.Audit.Infrastructure.Persistence;
using UMS.Modules.Audit.IntegrationTests.Infrastructure;
using UMS.Shared.Audit;

namespace UMS.Modules.Audit.IntegrationTests.Entries;

/// <summary>
/// AUD-8/edge-cases.md: "A query spans a partition boundary... must return a correctly merged,
/// ordered result." AuditPartitionMaintenanceService (started with the Host, see
/// AuditApiFixture) always creates last month's and this month's partitions
/// (monthsBehind: 1), so writing one entry dated last month and one dated this month for the
/// same entity, directly through the parent table, is a real cross-partition scenario - not a
/// simulation.
/// </summary>
[Collection(AuditApiTestCollectionDefinition.Name)]
public class EntityHistoryPartitionBoundaryTests(AuditApiFixture fixture)
{
    [Fact]
    public async Task History_spanning_two_monthly_partitions_returns_both_entries_correctly_ordered()
    {
        using var client = fixture.CreateClient();
        var (_, adminUsername, adminPassword, _) = await TestDataSeeder.ProvisionAdminAsync(fixture, client);
        var accessToken = await TestAuth.LoginAsync(client, adminUsername, adminPassword);

        var entityId = $"boundary-{Guid.NewGuid():N}";
        var lastMonth = DateTimeOffset.UtcNow.AddMonths(-1);
        var thisMonth = DateTimeOffset.UtcNow;

        await InsertDirectlyAsync(entityId, "create", lastMonth);
        await InsertDirectlyAsync(entityId, "update", thisMonth);

        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/audit/entities/Grade/{entityId}/history").WithBearerToken(accessToken);
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var history = await response.Content.ReadFromJsonAsync<List<AuditLogEntryDto>>();

        Assert.Equal(2, history!.Count);
        Assert.Equal("create", history[0].Action);
        Assert.Equal("update", history[1].Action);
        Assert.True(history[0].OccurredAt < history[1].OccurredAt);
    }

    /// <summary>
    /// Bypasses IAuditRecorder deliberately: production code always stamps <c>occurredAt</c> as
    /// "now" (see RecordAuditEntryRequest's remarks), which makes it impossible to seed
    /// historical, previous-partition data through the public write path. Constructing the
    /// aggregate directly (still through its own validating factory, never a raw INSERT) and
    /// saving through a plain AuditDbContext bound to the test's own Postgres container is the
    /// only way to set up this specific edge case.
    /// </summary>
    private async Task InsertDirectlyAsync(string entityId, string action, DateTimeOffset occurredAt)
    {
        var options = new DbContextOptionsBuilder<AuditDbContext>()
            .UseNpgsql(fixture.PostgresConnectionString)
            .Options;

        await using var context = new AuditDbContext(options);
        var entry = AuditLogEntry.Record(
            actorId: "system:boundary-test",
            actorType: AuditActorType.System,
            ipAddress: null,
            application: "system",
            entityType: "Grade",
            entityId: entityId,
            action: action,
            beforeValueJson: null,
            afterValueJson: "{}",
            correlationId: $"boundary-{entityId}",
            reason: null,
            isCorrection: false,
            organizationScopeId: null,
            occurredAt: occurredAt);

        Assert.True(entry.IsSuccess);

        var dbSet = context.Set<AuditLogEntry>();
        dbSet.Add(entry.Value);
        await context.SaveChangesAsync();
    }
}
