using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Audit.Application.Abstractions;
using UMS.Modules.Audit.Application.Entries;
using UMS.Modules.Audit.IntegrationTests.Infrastructure;
using UMS.Modules.Identity.Application.Abstractions;
using UMS.Shared.Audit;

namespace UMS.Modules.Audit.IntegrationTests.Writing;

/// <summary>AUD-5: writing many entries for one bulk operation lands one row per affected entity, inside a single transaction - never a batch-summary row.</summary>
[Collection(AuditApiTestCollectionDefinition.Name)]
public class BulkWriteTests(AuditApiFixture fixture)
{
    [Fact]
    public async Task RecordEntriesAsync_writes_one_entry_per_affected_entity_in_one_transaction()
    {
        using var scope = fixture.Services.CreateScope();
        var identityUnitOfWork = scope.ServiceProvider.GetRequiredService<UMS.Modules.Identity.Application.Abstractions.IUnitOfWork>();
        var auditRecorder = scope.ServiceProvider.GetRequiredService<IAuditRecorder>();
        var entries = scope.ServiceProvider.GetRequiredService<IAuditLogEntryRepository>();

        var correlationId = $"bulk-{Guid.NewGuid():N}";
        const int rowCount = 25;

        var requests = Enumerable.Range(0, rowCount)
            .Select(i => new RecordAuditEntryRequest(
                ActorId: "system:bulk-import-test",
                ActorType: AuditActorType.System,
                IpAddress: null,
                Application: "system",
                EntityType: "Grade",
                EntityId: $"grade_{i}",
                Action: AuditActions.Create,
                BeforeValueJson: null,
                AfterValueJson: $$"""{"score":{{i}}}""",
                CorrelationId: correlationId))
            .ToList();

        await using var transaction = await identityUnitOfWork.BeginTransactionAsync();
        var result = await auditRecorder.RecordEntriesAsync(requests, transaction.DbTransaction);
        Assert.True(result.IsSuccess);
        await transaction.CommitAsync();

        var (items, total) = await entries.ListAsync(new AuditEntryFilter(), skip: 0, take: rowCount + 10);
        var written = items.Where(e => e.CorrelationId == correlationId).ToList();

        Assert.Equal(rowCount, written.Count);
        Assert.Equal(rowCount, written.Select(e => e.EntityId).Distinct().Count());
    }
}
