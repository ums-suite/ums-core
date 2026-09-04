using System.Data.Common;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Shared.Audit;

/// <summary>
/// AUD-1: Audit's sole write operation, exposed as an internal application-service interface
/// (ADR-0002/0003) any one of the other 17 modules calls directly, in-process - never over HTTP
/// (requirement-spec.md audit §6: "no other module calls Audit over HTTP, since they are all
/// in-process today"). There is deliberately no <c>Update</c>/<c>Delete</c> method anywhere on
/// this interface (ADR-0012).
///
/// <para>
/// Living in <c>UMS.Shared.Audit</c> - not <c>UMS.Modules.Audit.*</c> - is what lets every other
/// module call it without taking a forbidden dependency on Audit's Domain/Application/
/// Infrastructure internals (module-boundaries.md, ADR-0002), mirroring the same pattern
/// <c>UMS.Shared.Authorization.IPermissionManifest</c> already established for Identity's
/// catalog fan-in. Audit's own Infrastructure layer (<c>UMS.Modules.Audit.Infrastructure</c>)
/// registers the one real implementation against this interface at composition-root time.
/// </para>
///
/// <para>
/// <b>Atomicity mechanism</b> (requirement-spec.md audit §2: "callable within the caller's own
/// database transaction"; ADR-0001's single-physical-database model; design-decisions.md's
/// "Write-Path Transaction Coupling Mechanism"): the caller passes the exact <see cref="DbTransaction"/>
/// its own <c>DbContext</c> already has open (via <c>context.Database.BeginTransactionAsync()</c>
/// or an already-ambient one from <c>context.Database.CurrentTransaction</c>). The implementation
/// binds a short-lived Audit <c>DbContext</c> to that same physical connection/transaction (EF
/// Core's documented cross-context-transaction pattern) and inserts through it - the two writes
/// share one PostgreSQL transaction and therefore commit or roll back together by construction,
/// never by caller discipline.
/// </para>
/// </summary>
public interface IAuditRecorder
{
    /// <summary>
    /// Records one sensitive mutation, in the same transaction as <paramref name="hostTransaction"/>.
    /// Per the atomicity invariant, a failure here must propagate to the caller (never be swallowed)
    /// so the caller's own <c>SaveChanges</c>/commit does not proceed without its audit trail
    /// (requirement-spec.md audit §4/§8).
    /// </summary>
    public Task<Result> RecordEntryAsync(
        RecordAuditEntryRequest request,
        DbTransaction hostTransaction,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// AUD-5: bulk-operation write support - writes one <c>AuditLogEntry</c> per affected entity
    /// (never one entry summarizing the whole batch) inside the same single transaction as
    /// <paramref name="hostTransaction"/> (requirement-spec.md audit §2/§8, "High-volume bulk
    /// operation... accepted as correct... not a design change").
    /// </summary>
    public Task<Result> RecordEntriesAsync(
        IReadOnlyCollection<RecordAuditEntryRequest> requests,
        DbTransaction hostTransaction,
        CancellationToken cancellationToken = default);
}
