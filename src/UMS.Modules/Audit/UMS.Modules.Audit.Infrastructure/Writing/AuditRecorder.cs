using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using UMS.Modules.Audit.Domain.Entries;
using UMS.Modules.Audit.Infrastructure.Persistence;
using UMS.Shared.Audit;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Audit.Infrastructure.Writing;

/// <summary>
/// AUD-1/AUD-5: the one real implementation of <see cref="IAuditRecorder"/>, registered against
/// that shared interface so every other module can call it without depending on
/// <c>UMS.Modules.Audit.*</c> internals (module-boundaries.md).
///
/// <para>
/// <b>Why this binds an ad hoc <see cref="AuditDbContext"/> to the caller's own transaction
/// instead of using a DI-scoped one:</b> a normal <c>AddDbContext</c>-registered context owns its
/// own pooled connection, which would be a *different* physical connection/transaction than the
/// caller's - two separate PostgreSQL transactions can never be atomic with each other without a
/// distributed-transaction coordinator (unavailable/impractical here, see
/// <c>IAuditRecorder</c>'s remarks). Binding to <paramref name="hostTransaction"/>'s own
/// <see cref="DbTransaction.Connection"/> is EF Core's own documented cross-context-transaction
/// recipe and is what actually makes "commit together or not at all" (ADR-0012) true by
/// construction.
/// </para>
/// </summary>
internal sealed class AuditRecorder : IAuditRecorder
{
    public async Task<Result> RecordEntryAsync(RecordAuditEntryRequest request, DbTransaction hostTransaction, CancellationToken cancellationToken = default)
    {
        var entryResult = BuildEntry(request);
        if (entryResult.IsFailure)
        {
            return Result.Failure(entryResult.Error!);
        }

        await using var context = CreateBoundContext(hostTransaction);
        context.Entries.Add(entryResult.Value);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    public async Task<Result> RecordEntriesAsync(IReadOnlyCollection<RecordAuditEntryRequest> requests, DbTransaction hostTransaction, CancellationToken cancellationToken = default)
    {
        if (requests.Count == 0)
        {
            return Result.Success();
        }

        var entries = new List<AuditLogEntry>(requests.Count);
        foreach (var request in requests)
        {
            var entryResult = BuildEntry(request);
            if (entryResult.IsFailure)
            {
                return Result.Failure(entryResult.Error!);
            }

            entries.Add(entryResult.Value);
        }

        // AUD-5: one AuditLogEntry per affected entity, all inside the same single transaction as
        // the caller's own bulk mutation (requirement-spec.md audit §2/§8) - never a batch-level
        // summary row.
        await using var context = CreateBoundContext(hostTransaction);
        context.Entries.AddRange(entries);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    private static Result<AuditLogEntry> BuildEntry(RecordAuditEntryRequest request) => AuditLogEntry.Record(
        request.ActorId,
        request.ActorType,
        request.IpAddress,
        request.Application,
        request.EntityType,
        request.EntityId,
        request.Action,
        request.BeforeValueJson,
        request.AfterValueJson,
        request.CorrelationId,
        request.Reason,
        request.IsCorrection,
        request.OrganizationScopeId,
        DateTimeOffset.UtcNow);

    private static AuditDbContext CreateBoundContext(DbTransaction hostTransaction)
    {
        if (hostTransaction.Connection is not NpgsqlConnection connection)
        {
            throw new InvalidOperationException(
                "IAuditRecorder requires a transaction opened against a Npgsql (PostgreSQL) connection - RecordEntry can only share the exact same physical transaction as its caller (ADR-0012), never a different provider/connection.");
        }

        var options = new DbContextOptionsBuilder<AuditDbContext>()
            .UseNpgsql(connection)
            .Options;

        var context = new AuditDbContext(options);
        context.Database.UseTransaction(hostTransaction);
        return context;
    }
}
